// -----------------------------------------------------------------------------
// DeltaTcpLib.cs
// A reusable .NET Standard library for delta-based TCP messaging
// -----------------------------------------------------------------------------
using RavenNest.BusinessLogic.Game;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RavenNest.Models;
using RavenNest.Models.TcpApi;
using Microsoft.Extensions.Options;

namespace RavenNest.BusinessLogic.Net.DeltaTcpLib
{
    public interface ISessionTokenProvider
    {
        SessionToken Get(string rawToken);
    }

    // -------------------------------------------------------------------------
    // Delta handler callbacks
    // -------------------------------------------------------------------------
    public interface IDeltaHandler
    {
        void OnExperienceDelta(SessionToken session, IReadOnlyList<DeltaExperienceUpdate> deltas);
        void OnPlayerStateDelta(SessionToken session, IReadOnlyList<CharacterStateDelta> deltas);
        void OnGameState(SessionToken session, GameStateRequest state);
    }

    // -------------------------------------------------------------------------
    // Game state models
    // -------------------------------------------------------------------------

    public class GameStateRequest
    {
        public int PlayerCount;
        public DungeonState Dungeon;
        public RaidState Raid;
    }

    // -------------------------------------------------------------------------
    // Delta data structures
    // -------------------------------------------------------------------------
    public struct SkillDelta
    {
        public byte Index;
        public long Experience;
        public short Level;
    }

    public struct DeltaExperienceUpdate
    {
        public Guid CharacterId;
        public uint DirtyMask;
        public SkillDelta[] Changes;
    }

    public struct CharacterStateDelta
    {
        public Guid CharacterId;
        public uint DirtyMask;

        public short Health;
        public Island Island;
        public Island Destination;
        public CharacterFlags State;
        public int TrainingSkillIndex;
        public string TaskArgument;
        public long ExpPerHour;
        public DateTime EstimatedTimeForLevelUp;
        public short X, Y, Z;
        public int AutoJoinRaidCounter;
        public int AutoJoinDungeonCounter;
        public long AutoJoinRaidCount;
        public long AutoJoinDungeonCount;
        public bool IsAutoResting;
        public int AutoTrainTargetLevel;
        public double? AutoRestTarget;
        public double? AutoRestStart;
        public int? DungeonCombatStyle;
        public int? RaidCombatStyle;

        public bool HasValue(CharacterStateFields field) =>
            (DirtyMask & (uint)field) != 0;

        public bool IsFullUpdate => DirtyMask == (uint)CharacterStateFields.All;
    }

    [Flags]
    public enum CharacterStateFields : uint
    {
        Health = 1 << 0,
        Island = 1 << 1,
        Destination = 1 << 2,
        State = 1 << 3,
        TrainingSkill = 1 << 4,
        TaskArgument = 1 << 5,
        ExpPerHour = 1 << 6,
        LevelUpETA = 1 << 7,
        Position = 1 << 8,    // X, Y, Z grouped together
        AutoJoinRaid = 1 << 9,   // Counter and Count grouped
        AutoJoinDungeon = 1 << 10, // Counter and Count grouped
        IsAutoResting = 1 << 11,
        AutoTrainLevel = 1 << 12,
        AutoRestTarget = 1 << 13,
        AutoRestStart = 1 << 14,
        DungeonStyle = 1 << 15,
        RaidStyle = 1 << 16,

        All = Health | Island | Destination | State | TrainingSkill | TaskArgument |
              ExpPerHour | LevelUpETA | Position | AutoJoinRaid | AutoJoinDungeon |
              IsAutoResting | AutoTrainLevel | AutoRestTarget | AutoRestStart |
              DungeonStyle | RaidStyle
    }

    // -------------------------------------------------------------------------
    // VarInt and Span reader/writer
    // -------------------------------------------------------------------------
    public static class VarInt
    {
        public static int WriteVarUInt(Span<byte> buf, ulong v)
        {
            int i = 0;
            while (v >= 0x80)
            {
                buf[i++] = (byte)(v | 0x80);
                v >>= 7;
            }
            buf[i++] = (byte)v;
            return i;
        }
        public static (ulong, int) ReadVarUInt(ReadOnlySpan<byte> buf)
        {
            ulong r = 0; int s = 0, i = 0; byte b;
            do { b = buf[i]; r |= (ulong)(b & 0x7F) << s; s += 7; i++; } while ((b & 0x80) != 0);
            return (r, i);
        }
    }
    public static class SpanReader
    {
        public static ulong ReadVarUInt(this ReadOnlySpan<byte> span, ref int pos)
        { var (v, i) = VarInt.ReadVarUInt(span.Slice(pos)); pos += i; return v; }
        public static byte ReadByte(this ReadOnlySpan<byte> span, ref int pos) => span[pos++];
        public static Guid ReadGuid(this ReadOnlySpan<byte> span, ref int pos)
        { var g = new Guid(span.Slice(pos, 16)); pos += 16; return g; }
        public static uint ReadUInt32BE(this ReadOnlySpan<byte> span, ref int pos)
        { var v = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(pos, 4)); pos += 4; return v; }
        public static short ReadInt16BE(this ReadOnlySpan<byte> span, ref int pos)
        { var v = BinaryPrimitives.ReadInt16BigEndian(span.Slice(pos, 2)); pos += 2; return v; }
        public static float ReadFloatBE(this ReadOnlySpan<byte> span, ref int pos)
        { var bits = BinaryPrimitives.ReadInt32BigEndian(span.Slice(pos, 4)); pos += 4; return BitConverter.Int32BitsToSingle(bits); }
        public static bool ReadBool(this ReadOnlySpan<byte> span, ref int pos) => span[pos++] != 0;
        public static string ReadString(this ReadOnlySpan<byte> span, ref int pos)
        {
            var len = (int)span.ReadVarUInt(ref pos);
            var s = Encoding.UTF8.GetString(span.Slice(pos, len)); pos += len;
            return s;
        }
        public static DateTime ReadDateTime(this ReadOnlySpan<byte> span, ref int pos)
        {
            var ticks = (long)span.ReadVarUInt(ref pos);
            return new DateTime(ticks, DateTimeKind.Utc);
        }
        public static Island ReadIsland(this ReadOnlySpan<byte> span, ref int pos) => (Island)span[pos++];
        public static CharacterFlags ReadFlags(this ReadOnlySpan<byte> span, ref int pos)
        { var v = BinaryPrimitives.ReadInt32BigEndian(span.Slice(pos, 4)); pos += 4; return (CharacterFlags)v; }
    }

    public static class SpanWriter
    {
        public static int Write(this Span<byte> span, Guid g)
        { g.ToByteArray().CopyTo(span); return 16; }
        public static int Write(this Span<byte> span, ulong v) => VarInt.WriteVarUInt(span, v);
        public static int Write(this Span<byte> span, uint v)
        { BinaryPrimitives.WriteUInt32BigEndian(span, v); return 4; }
        public static int Write(this Span<byte> span, short v)
        { BinaryPrimitives.WriteInt16BigEndian(span, v); return 2; }
        public static int Write(this Span<byte> span, float v)
        { var b = BitConverter.SingleToInt32Bits(v); BinaryPrimitives.WriteInt32BigEndian(span, b); return 4; }
        public static int Write(this Span<byte> span, bool v) { span[0] = (byte)(v ? 1 : 0); return 1; }
        public static int Write(this Span<byte> span, string s)
        { var b = Encoding.UTF8.GetBytes(s); int p = VarInt.WriteVarUInt(span, (ulong)b.Length); b.CopyTo(span.Slice(p)); return p + b.Length; }
        public static int Write(this Span<byte> span, DateTime dt) => VarInt.WriteVarUInt(span, (ulong)dt.Ticks);
        public static int Write(this Span<byte> span, Island i) { span[0] = (byte)i; return 1; }
        public static int Write(this Span<byte> span, CharacterFlags f)
        { BinaryPrimitives.WriteInt32BigEndian(span, (int)f); return 4; }
    }

    // -------------------------------------------------------------------------
    // TCP Server
    // -------------------------------------------------------------------------
    public class DeltaServer : IDisposable
    {
        private readonly Socket _listener;
        private readonly AppSettings? _settings;
        private readonly int _serverPort;
        private readonly ISessionTokenProvider _sessionTokenProvider;
        private readonly IDeltaHandler _handler;
        private readonly ConcurrentDictionary<Socket, SessionToken> _sessions = new();
        private CancellationTokenSource _cts;

        public DeltaServer(
            AppSettings settings,
            ISessionTokenProvider sessionTokenProvider,
            IDeltaHandler handler)
        {
            _settings = settings;
            _serverPort = (_settings?.TcpApiPort > 0) ? _settings.TcpApiPort + 1 : 3921;
            _sessionTokenProvider = sessionTokenProvider;
            _handler = handler;
            _listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _listener.Bind(new IPEndPoint(IPAddress.Any, _serverPort));
            _listener.Listen(1000);
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


        private void ParsePlayerState(ReadOnlySpan<byte> span, ref int pos, SessionToken session)
        {
            int cnt = (int)span.ReadVarUInt(ref pos);
            var list = new List<CharacterStateDelta>(cnt);
            for (int i = 0; i < cnt; i++)
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

                // Read fields that are marked as dirty
                if ((dirtyMask & (uint)CharacterStateFields.Health) != 0)
                    d.Health = span.ReadInt16BE(ref pos);

                if ((dirtyMask & (uint)CharacterStateFields.Island) != 0)
                    d.Island = span.ReadIsland(ref pos);

                if ((dirtyMask & (uint)CharacterStateFields.Destination) != 0)
                    d.Destination = span.ReadIsland(ref pos);

                if ((dirtyMask & (uint)CharacterStateFields.State) != 0)
                    d.State = span.ReadFlags(ref pos);

                if ((dirtyMask & (uint)CharacterStateFields.TrainingSkill) != 0)
                    d.TrainingSkillIndex = (int)span.ReadVarUInt(ref pos);

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

                list.Add(d);
            }

            _handler.OnPlayerStateDelta(session, list);
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
            return read;
        }

        public void Dispose()
        {
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
