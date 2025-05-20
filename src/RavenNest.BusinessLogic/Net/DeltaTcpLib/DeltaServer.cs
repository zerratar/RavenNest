// -----------------------------------------------------------------------------
// DeltaTcpLib.cs
// A reusable .NET Standard library for delta-based TCP messaging
// -----------------------------------------------------------------------------
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Models.TcpApi;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Net.DeltaTcpLib
{
    // -------------------------------------------------------------------------
    // TCP Server
    // -------------------------------------------------------------------------
    public class DeltaServer : IDisposable
    {
        private readonly Socket _listener;
        private readonly ILogger<DeltaServer> logger;
        private readonly AppSettings? _settings;
        private readonly GameData gameData;
        private readonly int _serverPort;
        private readonly ISessionTokenProvider _sessionTokenProvider;
        private readonly IDeltaHandler _handler;
        private readonly ConcurrentDictionary<Socket, SessionToken> _sessions = new();
        private CancellationTokenSource _cts;

        // Network statistics fields
        private long _messagesIn;
        private long _bytesIn;
        private long _messagesOut;
        private long _bytesOut;

        private double _lastReceiveRateKBps;
        private double _lastSendRateKBps;
        private long _lastMessagesIn;
        private long _lastBytesIn;
        private long _lastMessagesOut;
        private long _lastBytesOut;
        private Stopwatch _statsWatch = Stopwatch.StartNew();

        private Timer? _statsTimer;

        public DeltaServer(
            ILogger<DeltaServer> logger,
            AppSettings settings,
            GameData gameData,
            ISessionTokenProvider sessionTokenProvider,
            IDeltaHandler handler)
        {
            _settings = settings;
            this.logger = logger;
            this.gameData = gameData;
            _serverPort = (_settings?.TcpApiPort > 0) ? _settings.TcpApiPort + 1 : 3921;
            _sessionTokenProvider = sessionTokenProvider;
            _handler = handler;
            _listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _listener.Bind(new IPEndPoint(IPAddress.Any, _serverPort));
            _listener.Listen(1000);

            _statsTimer = new Timer(UpdateNetworkStats, null, 1000, 1000);

        }

        private void UpdateNetworkStats(object? state)
        {
            var elapsedSec = Math.Max(_statsWatch.Elapsed.TotalSeconds, 1.0);

            var messagesIn = Interlocked.Read(ref _messagesIn);
            var bytesIn = Interlocked.Read(ref _bytesIn);
            var messagesOut = Interlocked.Read(ref _messagesOut);
            var bytesOut = Interlocked.Read(ref _bytesOut);

            var deltaMessagesIn = messagesIn - _lastMessagesIn;
            var deltaBytesIn = bytesIn - _lastBytesIn;
            var deltaMessagesOut = messagesOut - _lastMessagesOut;
            var deltaBytesOut = bytesOut - _lastBytesOut;

            _lastReceiveRateKBps = deltaBytesIn / 1024.0;
            _lastSendRateKBps = deltaBytesOut / 1024.0;

            _lastMessagesIn = messagesIn;
            _lastBytesIn = bytesIn;
            _lastMessagesOut = messagesOut;
            _lastBytesOut = bytesOut;

            // Update GameData stats
            if (gameData != null)
            {
                gameData.SetDeltaServerNetworkStats(
                    Environment.CurrentManagedThreadId,
                    messagesIn, _lastReceiveRateKBps,
                    messagesOut, _lastSendRateKBps
                );
            }

            Interlocked.Exchange(ref _messagesIn, 0);
            Interlocked.Exchange(ref _messagesOut, 0);

            _statsWatch.Restart();
        }
        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(AcceptLoop);
        }

        public void Stop() => _cts.Cancel();

        private async Task AcceptLoop()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var client = await Task.Factory.FromAsync(_listener.BeginAccept, _listener.EndAccept, null);
                Task.Run(() => ClientLoop(client));
            }
        }

        private void ClientLoop(Socket client)
        {
            var buf4 = new byte[4];
            try
            {
                while (client.Connected)
                {
                    if (ReceiveExact(client, buf4) != 4) break;
                    int len = BinaryPrimitives.ReadInt32BigEndian(buf4);
                    var buf = ArrayPool<byte>.Shared.Rent(len);
                    try
                    {
                        if (ReceiveExact(client, buf, len) != len) break;
                        byte type = buf[0];
                        var span = buf.AsSpan(1, len - 1);
                        int pos = 0;
                        if (type == 1)
                        {
                            var raw = Encoding.UTF8.GetString(span);
                            var tok = _sessionTokenProvider.Get(raw);
                            if (tok == null) break;
                            _sessions[client] = tok;
                        }
                        else if (_sessions.TryGetValue(client, out var session))
                        {
                            if (type == 2) ParseExperience(span, ref pos, session);
                            else if (type == 3) ParsePlayerState(span, ref pos, session);
                            else if (type == 4) ParseGameState(span, ref pos, session);
                        }
                    }
                    finally { ArrayPool<byte>.Shared.Return(buf); }
                }
            }
            catch { }
            finally { client.Close(); }
        }

        private void ParseExperience(ReadOnlySpan<byte> span, ref int pos, SessionToken session)
        {
            try
            {
                int cnt = (int)span.ReadVarUInt(ref pos);
                var list = new List<DeltaExperienceUpdate>(cnt);
                for (int i = 0; i < cnt; i++)
                {
                    var cid = span.ReadGuid(ref pos);
                    var mask = span.ReadUInt32BE(ref pos);
                    int cc = (int)span.ReadVarUInt(ref pos);
                    var arr = new SkillDelta[cc];
                    for (int j = 0; j < cc; j++)
                        arr[j] = new SkillDelta
                        {
                            Index = span.ReadByte(ref pos),
                            Experience = (long)span.ReadVarUInt(ref pos),
                            Level = (short)span.ReadVarUInt(ref pos)
                        };
                    list.Add(new DeltaExperienceUpdate { CharacterId = cid, DirtyMask = mask, Changes = arr });
                }
                _handler.OnExperienceDelta(session, list);
            }
            catch (Exception exc)
            {
                logger?.LogError($"Failed to parse experience update from client ({session.UserName}): " + exc);
            }
        }


        private void ParsePlayerState(ReadOnlySpan<byte> span, ref int pos, SessionToken session)
        {
            try
            {
                int cnt = (int)span.ReadVarUInt(ref pos);
                var list = new List<CharacterStateDelta>(cnt);
                for (int i = 0; i < cnt; i++)
                {
                    try
                    {
                        // Read character ID (always present)
                        var characterId = span.ReadGuid(ref pos);

                        // Read the dirty mask
                        uint dirtyMask = span.ReadUInt32BE(ref pos);

                        // Initialize delta with default values
                        var d = new CharacterStateDelta
                        {
                            CharacterId = characterId,
                            DirtyMask = dirtyMask
                        };

                        if (session.ClientVersion.Contains("9.4.0"))
                        {
                            if (!ReadPlayerStateV940(ref d, span, ref pos, dirtyMask, session, characterId))
                            {
                                break;
                            }
                        }
                        else
                        {
                            if (!ReadPlayerState(ref d, span, ref pos, dirtyMask, session, characterId))
                            {
                                break;
                            }
                        }

                        list.Add(d);
                    }
                    catch (Exception exc)
                    {
                        logger?.LogError($"Failed to parse player state update from client ({session.UserName}). {i}/{cnt} players processed. Error: " + exc);
                        break;
                    }
                }
                if (list.Count > 0)
                {
                    _handler.OnPlayerStateDelta(session, list);
                }
            }
            catch (Exception exc)
            {
                logger?.LogError($"Failed to parse player state update from client ({session.UserName}): " + exc);
            }
        }

        private bool ReadPlayerState(ref CharacterStateDelta d, ReadOnlySpan<byte> span, ref int pos, uint dirtyMask, SessionToken session, Guid characterId)
        {
            // Read fields that are marked as dirty
            if ((dirtyMask & (uint)CharacterStateFields.Health) != 0)
                d.Health = span.ReadInt16BE(ref pos);

            if ((dirtyMask & (uint)CharacterStateFields.Island) != 0)
                d.Island = span.ReadIsland(ref pos);
            // 24
            if ((dirtyMask & (uint)CharacterStateFields.Destination) != 0)
                d.Destination = span.ReadIsland(ref pos);
            // 25
            if ((dirtyMask & (uint)CharacterStateFields.State) != 0)
                d.State = span.ReadFlags(ref pos);
            // 29
            if ((dirtyMask & (uint)CharacterStateFields.TrainingSkill) != 0)
                d.TrainingSkillIndex = (int)span.ReadVarUInt(ref pos);
            // 39
            if ((dirtyMask & (uint)CharacterStateFields.TaskArgument) != 0)
                d.TaskArgument = span.ReadShortString(ref pos);

            if ((dirtyMask & (uint)CharacterStateFields.ExpPerHour) != 0)
                d.ExpPerHour = (long)span.ReadVarUInt(ref pos);

            if ((dirtyMask & (uint)CharacterStateFields.LevelUpETA) != 0)
                d.EstimatedTimeForLevelUp = span.ReadDateTime(ref pos);

            if ((dirtyMask & (uint)CharacterStateFields.Position) != 0)
            {
                d.X = span.ReadInt16BE(ref pos);
                d.Y = span.ReadInt16BE(ref pos);
                d.Z = span.ReadInt16BE(ref pos);
            }

            if ((dirtyMask & (uint)CharacterStateFields.AutoJoinRaid) != 0)
            {
                d.AutoJoinRaidCounter = (int)span.ReadVarUInt(ref pos);
                d.AutoJoinRaidCount = (long)span.ReadVarUInt(ref pos);
            }

            if ((dirtyMask & (uint)CharacterStateFields.AutoJoinDungeon) != 0)
            {
                d.AutoJoinDungeonCounter = (int)span.ReadVarUInt(ref pos);
                d.AutoJoinDungeonCount = (long)span.ReadVarUInt(ref pos);
            }

            if ((dirtyMask & (uint)CharacterStateFields.IsAutoResting) != 0)
                d.IsAutoResting = span.ReadBool(ref pos);

            if ((dirtyMask & (uint)CharacterStateFields.AutoTrainLevel) != 0)
                d.AutoTrainTargetLevel = (int)span.ReadVarUInt(ref pos);

            if ((dirtyMask & (uint)CharacterStateFields.AutoRestTarget) != 0)
            {
                if (span.ReadBool(ref pos))
                {
                    var bits = BinaryPrimitives.ReadInt64BigEndian(span.Slice(pos, 8));
                    pos += 8;
                    d.AutoRestTarget = BitConverter.Int64BitsToDouble(bits);
                }
                else
                {
                    d.AutoRestTarget = null;
                }
            }

            if ((dirtyMask & (uint)CharacterStateFields.AutoRestStart) != 0)
            {
                if (span.ReadBool(ref pos))
                {
                    var bits = BinaryPrimitives.ReadInt64BigEndian(span.Slice(pos, 8));
                    pos += 8;
                    d.AutoRestStart = BitConverter.Int64BitsToDouble(bits);
                }
                else
                {
                    d.AutoRestStart = null;
                }
            }

            if ((dirtyMask & (uint)CharacterStateFields.DungeonStyle) != 0)
            {
                if (span.ReadBool(ref pos))
                    d.DungeonCombatStyle = (int)span.ReadVarUInt(ref pos);
                else
                    d.DungeonCombatStyle = null;
            }

            if ((dirtyMask & (uint)CharacterStateFields.RaidStyle) != 0)
            {
                if (span.ReadBool(ref pos))
                    d.RaidCombatStyle = (int)span.ReadVarUInt(ref pos);
                else
                    d.RaidCombatStyle = null;
            }

            if ((dirtyMask & (uint)CharacterStateFields.Platform) != 0)
            {
                d.PlatformUserId = span.ReadShortString(ref pos);
                d.PlatformUserName = span.ReadShortString(ref pos);
            }

            if (characterId.ToString().StartsWith("000") || (!string.IsNullOrEmpty(d.TaskArgument) && d.TaskArgument.Contains('\0')))
            {
                return false; // broken
            }
            if ((!string.IsNullOrEmpty(d.PlatformUserName) && d.PlatformUserName.Contains('\0')))
            {
                // BROKEN!
                d.Platform = null;
                d.PlatformUserId = null;
                d.PlatformUserName = null;
            }
            return true;
        }

        private bool ReadPlayerStateV940(ref CharacterStateDelta d, ReadOnlySpan<byte> span, ref int pos, uint dirtyMask, SessionToken session, Guid characterId)
        {

            // Read fields that are marked as dirty
            if ((dirtyMask & (uint)CharacterStateFields.Health) != 0)
                d.Health = span.ReadInt16BE(ref pos);

            if ((dirtyMask & (uint)CharacterStateFields.Island) != 0)
                d.Island = span.ReadIsland(ref pos);
            // 24
            if ((dirtyMask & (uint)CharacterStateFields.Destination) != 0)
                d.Destination = span.ReadIsland(ref pos);
            // 25
            if ((dirtyMask & (uint)CharacterStateFields.State) != 0)
                d.State = span.ReadFlags(ref pos);
            // 29
            if ((dirtyMask & (uint)CharacterStateFields.TrainingSkill) != 0)
                d.TrainingSkillIndex = (int)span.ReadVarUInt(ref pos);
            // 39
            if ((dirtyMask & (uint)CharacterStateFields.TaskArgument) != 0)
                d.TaskArgument = span.ReadString(ref pos);

            if ((dirtyMask & (uint)CharacterStateFields.ExpPerHour) != 0)
                d.ExpPerHour = (long)span.ReadVarUInt(ref pos);

            if ((dirtyMask & (uint)CharacterStateFields.LevelUpETA) != 0)
                d.EstimatedTimeForLevelUp = span.ReadDateTime(ref pos);

            if ((dirtyMask & (uint)CharacterStateFields.Position) != 0)
            {
                d.X = span.ReadInt16BE(ref pos);
                d.Y = span.ReadInt16BE(ref pos);
                d.Z = span.ReadInt16BE(ref pos);
            }

            if ((dirtyMask & (uint)CharacterStateFields.AutoJoinRaid) != 0)
            {
                d.AutoJoinRaidCounter = (int)span.ReadFloatBE(ref pos);
                d.AutoJoinRaidCount = (long)span.ReadVarUInt(ref pos);
            }

            if ((dirtyMask & (uint)CharacterStateFields.AutoJoinDungeon) != 0)
            {
                d.AutoJoinDungeonCounter = (int)span.ReadFloatBE(ref pos);
                d.AutoJoinDungeonCount = (long)span.ReadVarUInt(ref pos);
            }

            if ((dirtyMask & (uint)CharacterStateFields.IsAutoResting) != 0)
                d.IsAutoResting = span.ReadBool(ref pos);

            if ((dirtyMask & (uint)CharacterStateFields.AutoTrainLevel) != 0)
                d.AutoTrainTargetLevel = (int)span.ReadFloatBE(ref pos);

            if ((dirtyMask & (uint)CharacterStateFields.AutoRestTarget) != 0)
            {
                if (span.ReadBool(ref pos))
                {
                    var bits = BinaryPrimitives.ReadInt64BigEndian(span.Slice(pos, 8));
                    pos += 8;
                    d.AutoRestTarget = BitConverter.Int64BitsToDouble(bits);
                }
                else
                {
                    d.AutoRestTarget = null;
                }
            }

            if ((dirtyMask & (uint)CharacterStateFields.AutoRestStart) != 0)
            {
                if (span.ReadBool(ref pos))
                {
                    var bits = BinaryPrimitives.ReadInt64BigEndian(span.Slice(pos, 8));
                    pos += 8;
                    d.AutoRestStart = BitConverter.Int64BitsToDouble(bits);
                }
                else
                {
                    d.AutoRestStart = null;
                }
            }

            if ((dirtyMask & (uint)CharacterStateFields.DungeonStyle) != 0)
            {
                if (span.ReadBool(ref pos))
                    d.DungeonCombatStyle = (int)span.ReadVarUInt(ref pos);
                else
                    d.DungeonCombatStyle = null;
            }

            if ((dirtyMask & (uint)CharacterStateFields.RaidStyle) != 0)
            {
                if (span.ReadBool(ref pos))
                    d.RaidCombatStyle = (int)span.ReadVarUInt(ref pos);
                else
                    d.RaidCombatStyle = null;
            }

            try
            {
                if ((dirtyMask & (uint)CharacterStateFields.Platform) != 0)
                    d.Platform = span.ReadString(ref pos);

                if ((dirtyMask & (uint)CharacterStateFields.PlatformUserId) != 0)
                    d.PlatformUserId = span.ReadString(ref pos);

                if ((dirtyMask & (uint)CharacterStateFields.PlatformUserName) != 0)
                    d.PlatformUserName = span.ReadString(ref pos);
            }
            catch (Exception ex)
            {
                logger?.LogError($"Failed to parse platform data for character {characterId}: {ex}");
            }
            if (characterId.ToString().StartsWith("000") || (!string.IsNullOrEmpty(d.TaskArgument) && d.TaskArgument.Contains('\0')))
            {
                return false; // broken
            }
            if ((!string.IsNullOrEmpty(d.PlatformUserName) && d.PlatformUserName.Contains('\0')))
            {
                // BROKEN!
                d.Platform = null;
                d.PlatformUserId = null;
                d.PlatformUserName = null;
            }
            return true;
        }

        private void ParseGameState(ReadOnlySpan<byte> span, ref int pos, SessionToken session)
        {
            var gs = new GameStateRequest();
            gs.PlayerCount = (int)span.ReadVarUInt(ref pos);
            var isRaidActive = span.ReadBool(ref pos);

            if (isRaidActive)
            {
                gs.Raid = new RaidState
                {
                    IsActive = isRaidActive,
                    BossCombatLevel = (int)span.ReadVarUInt(ref pos),
                    CurrentBossHealth = (int)span.ReadVarUInt(ref pos),
                    MaxBossHealth = (int)span.ReadVarUInt(ref pos),
                    PlayersJoined = (int)span.ReadVarUInt(ref pos),
                    EndTime = span.ReadDateTime(ref pos)
                };
            }
            else
            {
                gs.Raid = new RaidState();
            }

            gs.Raid.NextRaid = span.ReadDateTime(ref pos);

            var isDungeonActive = span.ReadBool(ref pos);
            if (isDungeonActive)
            {
                var nameLen = (int)span.ReadVarUInt(ref pos);
                var dungeonName = string.Empty;
                if (nameLen > 0)
                {
                    dungeonName = Encoding.UTF8.GetString(span.Slice(pos, nameLen)); pos += nameLen;
                }

                gs.Dungeon = new DungeonState
                {
                    IsActive = isDungeonActive,
                    Name = dungeonName,
                    HasStarted = span.ReadBool(ref pos),
                    BossCombatLevel = (int)span.ReadVarUInt(ref pos),
                    CurrentBossHealth = (int)span.ReadVarUInt(ref pos),
                    MaxBossHealth = (int)span.ReadVarUInt(ref pos),
                    PlayersAlive = (int)span.ReadVarUInt(ref pos),
                    PlayersJoined = (int)span.ReadVarUInt(ref pos),
                    EnemiesLeft = (int)span.ReadVarUInt(ref pos),
                    StartTime = span.ReadDateTime(ref pos),
                };
            }
            else
            {
                gs.Dungeon = new DungeonState();
            }

            gs.Dungeon.NextDungeon = span.ReadDateTime(ref pos);

            _handler.OnGameState(session, gs);
        }

        private int ReceiveExact(Socket s, byte[] buf, int need = 4)
        {
            int read = 0;
            while (read < need)
            {
                int r = s.Receive(buf, read, need - read, SocketFlags.None);
                if (r <= 0) return read;
                read += r;
            }

            Interlocked.Increment(ref _messagesIn);
            Interlocked.Add(ref _bytesIn, read);
            return read;
        }

        public void Dispose()
        {
            //logger.LogDebug("Disposing Delta TCP Server..");
            Stop();
            try
            {
                try { _listener.Close(); } catch { }
                try { _listener.Dispose(); } catch { }
                try { _cts?.Dispose(); } catch { }

                try { }
                catch
                {
                    foreach (var session in _sessions)
                    {
                        session.Key.Close();
                        session.Key.Dispose();
                    }
                }

                _sessions.Clear();
            }
            catch { }
        }
    }
}
