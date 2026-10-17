using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Net.DeltaTcpLib;
using RavenNest.Models;
using RavenNest.Models.TcpApi;
using RavenNest.Protocol;
using GameStateRequest = RavenNest.BusinessLogic.Net.DeltaTcpLib.GameStateRequest;

namespace RavenNest.BusinessLogic.Net.V2
{
    /// <summary>
    /// One streamer's game session on the v2 connection. A single worker owns its state (the live connection,
    /// player save versions, the event log) and handles one thing at a time from its queue, in order, so none of
    /// it needs locks. Commands run alongside, on the thread pool, and post their results back.
    /// </summary>
    public sealed class GameSessionActor
    {
        /// <summary>A session with no connection for this long ends, and its event log is deleted.</summary>
        public static readonly TimeSpan EndAfterIdle = TimeSpan.FromMinutes(30);

        private const int MaxEventsPerBatch = 500;
        private const int MaxCommandsInFlight = 256;
        private const int MaxCommandsRunning = 8;

        private readonly GameConnectionHub hub;
        private readonly ILogger logger;
        private readonly Channel<Action> inbox = Channel.CreateUnbounded<Action>(new UnboundedChannelOptions { SingleReader = true });
        private readonly Dictionary<Guid, PlayerSaveState> saves = new Dictionary<Guid, PlayerSaveState>();
        private readonly ConcurrentDictionary<Guid, CommandEntry> commands = new ConcurrentDictionary<Guid, CommandEntry>();
        private readonly SemaphoreSlim commandSlots = new SemaphoreSlim(MaxCommandsRunning);
        private readonly SessionEventLog events;

        private GameConnection connection;
        private bool welcomed;
        private long eventsSentThrough;
        private DateTime idleSince = DateTime.UtcNow;
        private bool ended;

        public GameSessionActor(GameConnectionHub hub, SessionToken token, SessionEventLog events, ILogger logger)
        {
            this.hub = hub;
            this.logger = logger;
            this.events = events;
            Token = token;
            _ = Task.Run(RunAsync);
        }

        public Guid SessionId => Token.SessionId;
        public Guid UserId => Token.UserId;

        /// <summary>The session token the rest of the server works with; refreshed on every Hello.</summary>
        public SessionToken Token { get; private set; }

        /// <summary>The game run's resume id, from its last Hello. Kept in the event log, so it survives a restart.</summary>
        public Guid ResumeId
        {
            get => events.ResumeId;
            private set => events.ResumeId = value;
        }

        /// <summary>
        /// A stream's game loop keeps running this long after its last connection went, so a short drop does not
        /// stop raids, dungeons and item drops for everyone.
        /// </summary>
        public static readonly TimeSpan GameLoopGrace = TimeSpan.FromSeconds(60);

        private readonly List<(int ConnectionId, DateTime Closed)> closedConnections = new List<(int, DateTime)>();

        public bool Ended => ended;

        public bool IsConnected => connection != null && connection.IsOpen;

        public int UnconfirmedEvents => events.Count;

        private sealed class PlayerSaveState
        {
            public long Accepted;
            public DateTime AcceptedAt;
            public long Stored;
        }

        private sealed class CommandEntry
        {
            public Task<CommandResult> Task;
            public DateTime Created;
        }

        public void Post(Action work)
        {
            if (!inbox.Writer.TryWrite(work))
            {
                logger.LogWarning("v2 session " + SessionId + " has ended; dropped work.");
            }
        }

        private async Task RunAsync()
        {
            var reader = inbox.Reader;
            try
            {
                while (await reader.WaitToReadAsync())
                {
                    while (reader.TryRead(out var work))
                    {
                        try
                        {
                            work();
                        }
                        catch (Exception exc)
                        {
                            logger.LogError("v2 session " + SessionId + " failed handling work: " + exc);
                        }
                    }

                    // send what accumulated while draining in one go, and get the log to the disk
                    try
                    {
                        SendPendingEvents();
                        events.Flush();
                    }
                    catch (Exception exc)
                    {
                        logger.LogError("v2 session " + SessionId + " could not send events: " + exc);
                    }
                }
            }
            catch (Exception exc)
            {
                logger.LogError("v2 session " + SessionId + " worker stopped: " + exc);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Connections
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A connection said Hello with a valid token for this session.</summary>
        public void Attach(GameConnection newConnection, Hello hello, SessionToken token) => Post(() =>
        {
            if (ended)
            {
                newConnection.Reject(new Rejected { Reason = RejectReason.SessionEnded, Message = "The session has ended.", Reconnect = true });
                return;
            }

            if (connection != null && connection != newConnection)
            {
                connection.Reject(new Rejected { Reason = RejectReason.Replaced, Message = "A newer connection for this session took over." });
            }

            Token = token;
            connection = newConnection;
            hub.GameProcessors.Start(Token, newConnection.Id);

            // Resuming: the same game run (resume id) coming back. Its cursor says what it applied; anything after
            // that is sent again. A new game run starts from the log's confirmed point, as the old socket delivered
            // whatever waited for the streamer.
            var resumed = hello.ResumeId != Guid.Empty && hello.ResumeId == ResumeId;
            var restarted = hello.LastEventSequence > events.LastSequence; // the server's numbering started over
            if (resumed && !restarted)
            {
                events.Ack(hello.LastEventSequence);
                eventsSentThrough = hello.LastEventSequence;
            }
            else
            {
                eventsSentThrough = events.AckedThrough;
            }

            ResumeId = hello.ResumeId;
            welcomed = true;
            connection.Send(new Welcome
            {
                SessionId = SessionId,
                ServerTimeUtc = DateTime.UtcNow,
                Resumed = resumed && !restarted,
                ResyncRequired = !resumed || restarted,
                EventSequence = events.LastSequence
            });
        });

        /// <summary>A connection's read loop ended.</summary>
        public void Detach(GameConnection closed) => Post(() =>
        {
            // the game loop stops after the grace time (Tick), unless a new connection took over by then
            closedConnections.Add((closed.Id, DateTime.UtcNow));
            if (connection == closed)
            {
                connection = null;
                welcomed = false;
                idleSince = DateTime.UtcNow;
            }
        });

        /// <summary>A message from a connection of this session.</summary>
        public void Receive(GameConnection from, IMessage message) => Post(() =>
        {
            if (from != connection)
            {
                return; // a replaced connection still talking
            }

            switch (message)
            {
                case Ping ping:
                    connection.Send(new Pong { Timestamp = ping.Timestamp });
                    break;
                case Pong _:
                    break;
                case SaveBatch batch:
                    ApplySaves(batch);
                    break;
                case EventAck ack:
                    events.Ack(ack.Sequence);
                    break;
                case CommandRequest request:
                    StartCommand(request);
                    break;
                case Hello _:
                    from.Reject(new Rejected { Reason = RejectReason.BadMessage, Message = "Hello was already said." });
                    break;
            }
        });

        /// <summary>Once a second: pings, dead connections, idle end, old command results.</summary>
        public void Tick() => Post(() =>
        {
            var now = DateTime.UtcNow;
            if (connection != null)
            {
                if (now - connection.LastReceived > TimeSpan.FromMilliseconds(ProtocolInfo.DeadAfterMs))
                {
                    logger.LogInformation("v2 session " + SessionId + ": nothing received for " + ProtocolInfo.DeadAfterMs / 1000 + " s, closing the connection.");
                    connection.Close(WebSocketCloseStatus.EndpointUnavailable, "no traffic");
                }
                else if (now - connection.LastSent > TimeSpan.FromMilliseconds(ProtocolInfo.PingIntervalMs))
                {
                    connection.Send(new Ping { Timestamp = now.Ticks });
                }
            }
            else if (now - idleSince > EndAfterIdle)
            {
                EndNow(deleteLog: true);
                return;
            }

            for (var i = closedConnections.Count - 1; i >= 0; i--)
            {
                if (now - closedConnections[i].Closed > GameLoopGrace)
                {
                    hub.GameProcessors.Stop(Token, closedConnections[i].ConnectionId);
                    closedConnections.RemoveAt(i);
                }
            }

            foreach (var (id, entry) in commands)
            {
                if (entry.Task != null && entry.Task.IsCompleted && (now - entry.Created).TotalSeconds > ProtocolInfo.CommandResultKeepSeconds)
                {
                    commands.TryRemove(id, out _);
                }
            }
        });

        /// <summary>
        /// Ends the session: closes its connection, stops taking events, and hands back the events the game never
        /// confirmed (for a newer session of the same streamer to carry on with).
        /// </summary>
        public Task<List<SessionEvent>> EndAsync(bool deleteLog)
        {
            var done = new TaskCompletionSource<List<SessionEvent>>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!inbox.Writer.TryWrite(() => done.TrySetResult(EndNow(deleteLog))))
            {
                done.TrySetResult(new List<SessionEvent>());
            }
            return done.Task;
        }

        private List<SessionEvent> EndNow(bool deleteLog)
        {
            if (ended)
            {
                return new List<SessionEvent>();
            }

            ended = true;
            foreach (var (connectionId, _) in closedConnections)
            {
                hub.GameProcessors.Stop(Token, connectionId);
            }
            closedConnections.Clear();
            if (connection != null)
            {
                hub.GameProcessors.Stop(Token, connection.Id);
            }

            var left = events.After(events.AckedThrough, int.MaxValue);
            if (connection != null)
            {
                connection.Reject(new Rejected { Reason = RejectReason.SessionEnded, Message = "The session has ended.", Reconnect = true });
                connection = null;
            }

            if (deleteLog) events.Delete(); else events.Dispose();
            hub.Remove(this);
            inbox.Writer.TryComplete();
            return left;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Saves
        // ---------------------------------------------------------------------------------------------------------

        private void ApplySaves(SaveBatch batch)
        {
            if (batch.EventAck > 0)
            {
                events.Ack(batch.EventAck);
            }

            if (batch.GameState != null)
            {
                try
                {
                    hub.Players.OnGameState(Token, ToGameState(batch.GameState));
                }
                catch (Exception exc)
                {
                    logger.LogError("v2 session " + SessionId + ": game state failed: " + exc);
                }
            }

            if (batch.Players == null || batch.Players.Length == 0)
            {
                return;
            }

            // one answer per batch, listing only the saves that were refused (it was one ack per player before,
            // which with 1000 players was most of the traffic)
            var refused = new List<SaveAck>();
            List<SaveAck> stored = null;
            if (!hub.Players.TryGetSaveContext(Token, "players", out var context))
            {
                foreach (var save in batch.Players)
                {
                    refused.Add(new SaveAck { CharacterId = save.CharacterId, Version = save.Version, Status = SaveStatus.Invalid });
                }
                connection?.Send(new SaveAcks { Stage = SaveStage.Accepted, ThroughSequence = batch.Sequence, Acks = refused.ToArray() });
                return;
            }

            var now = DateTime.UtcNow;
            if (context.State != null)
            {
                context.State.LastExpRequest = now;
                context.State.LastStateRequest = now;
            }

            foreach (var save in batch.Players)
            {
                if (save == null || save.CharacterId == Guid.Empty)
                {
                    continue;
                }

                if (!saves.TryGetValue(save.CharacterId, out var state))
                {
                    saves[save.CharacterId] = state = new PlayerSaveState();
                }

                if (save.Version <= state.Accepted)
                {
                    // already have it (a resend after a lost ack): say again that it is stored, if it is
                    if (state.Stored >= save.Version)
                    {
                        (stored ??= new List<SaveAck>()).Add(new SaveAck { CharacterId = save.CharacterId, Version = state.Stored });
                    }
                    continue;
                }

                var status = SaveStatus.Ok;
                try
                {
                    if (save.Skills != null && save.Skills.Length > 0)
                    {
                        status = hub.Players.ApplyExperience(context, save.CharacterId, save.Skills);
                    }

                    if (status == SaveStatus.Ok && save.State != null && save.State.Changed != PlayerStateFields.None)
                    {
                        status = hub.Players.ApplyPlayerState(context, ToStateDelta(save.CharacterId, save.State));
                    }
                }
                catch (Exception exc)
                {
                    logger.LogError("v2 session " + SessionId + ": saving " + save.CharacterId + " failed: " + exc);
                    continue; // never stored, so the game sends it again
                }

                if (status == SaveStatus.Ok)
                {
                    state.Accepted = save.Version;
                    state.AcceptedAt = DateTime.UtcNow;
                }
                else
                {
                    refused.Add(new SaveAck { CharacterId = save.CharacterId, Version = save.Version, Status = status });
                }
            }

            connection?.Send(new SaveAcks { Stage = SaveStage.Accepted, ThroughSequence = batch.Sequence, Acks = refused.Count > 0 ? refused.ToArray() : null });
            if (stored != null)
            {
                connection?.Send(new SaveAcks { Stage = SaveStage.Stored, Acks = stored.ToArray() });
            }
        }

        /// <summary>A database save that started at <paramref name="started"/> finished.</summary>
        public void SaveCompleted(DateTime started, bool clean) => Post(() =>
        {
            List<SaveAck> stored = null;
            foreach (var (characterId, state) in saves)
            {
                if (state.Accepted <= state.Stored || state.AcceptedAt >= started)
                {
                    continue;
                }

                var character = hub.Data.GetCharacter(characterId);
                if (character == null || !hub.Data.IsSaved(character, clean))
                {
                    continue;
                }

                state.Stored = state.Accepted;
                (stored ??= new List<SaveAck>()).Add(new SaveAck { CharacterId = characterId, Version = state.Stored });
            }

            if (stored != null)
            {
                connection?.Send(new SaveAcks { Stage = SaveStage.Stored, Acks = stored.ToArray() });
            }
        });

        private static CharacterStateDelta ToStateDelta(Guid characterId, PlayerStateSave s)
        {
            return new CharacterStateDelta
            {
                CharacterId = characterId,
                DirtyMask = (uint)s.Changed,
                Health = (short)Math.Clamp(s.Health, short.MinValue, short.MaxValue),
                Island = (Island)s.Island,
                Destination = (Island)s.Destination,
                State = (CharacterFlags)s.State,
                TrainingSkillIndex = s.TrainingSkillIndex,
                TaskArgument = s.TaskArgument,
                ExpPerHour = s.ExpPerHour,
                EstimatedTimeForLevelUp = s.EstimatedTimeForLevelUp,
                X = ToShort(s.X),
                Y = ToShort(s.Y),
                Z = ToShort(s.Z),
                AutoJoinRaidCounter = s.AutoJoinRaidCounter,
                AutoJoinRaidCount = s.AutoJoinRaidCount,
                AutoJoinDungeonCounter = s.AutoJoinDungeonCounter,
                AutoJoinDungeonCount = s.AutoJoinDungeonCount,
                IsAutoResting = s.IsAutoResting,
                AutoTrainTargetLevel = s.AutoTrainTargetLevel,
                AutoRestTarget = s.AutoRestTarget,
                AutoRestStart = s.AutoRestStart,
                DungeonCombatStyle = s.DungeonCombatStyle,
                RaidCombatStyle = s.RaidCombatStyle,
                Platform = s.Platform,
                PlatformUserId = s.PlatformUserId,
                PlatformUserName = s.PlatformUserName
            };
        }

        private static short ToShort(float v)
        {
            if (float.IsNaN(v)) return 0;
            return (short)Math.Clamp(MathF.Round(v), short.MinValue, short.MaxValue);
        }

        private static GameStateRequest ToGameState(GameStateSave s)
        {
            return new GameStateRequest
            {
                PlayerCount = s.PlayerCount,
                Raid = s.Raid == null ? null : new RaidState
                {
                    IsActive = s.Raid.IsActive,
                    BossCombatLevel = s.Raid.BossCombatLevel,
                    CurrentBossHealth = s.Raid.CurrentBossHealth,
                    MaxBossHealth = s.Raid.MaxBossHealth,
                    PlayersJoined = s.Raid.PlayersJoined,
                    EndTime = s.Raid.EndTime,
                    NextRaid = s.Raid.NextRaid
                },
                Dungeon = s.Dungeon == null ? null : new DungeonState
                {
                    IsActive = s.Dungeon.IsActive,
                    Name = s.Dungeon.Name,
                    HasStarted = s.Dungeon.HasStarted,
                    BossCombatLevel = s.Dungeon.BossCombatLevel,
                    CurrentBossHealth = s.Dungeon.CurrentBossHealth,
                    MaxBossHealth = s.Dungeon.MaxBossHealth,
                    PlayersAlive = s.Dungeon.PlayersAlive,
                    PlayersJoined = s.Dungeon.PlayersJoined,
                    EnemiesLeft = s.Dungeon.EnemiesLeft,
                    StartTime = s.Dungeon.StartTime,
                    NextDungeon = s.Dungeon.NextDungeon
                }
            };
        }

        // ---------------------------------------------------------------------------------------------------------
        // Events
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A game event for this streamer, from any thread.</summary>
        public void AddEvent(int type, byte[] data) => Post(() =>
        {
            if (!ended)
            {
                events.Add(type, data, Guid.Empty);
            }
        });

        /// <summary>Events carried over from an older session of the same streamer, renumbered here.</summary>
        public void AddEvents(IReadOnlyList<SessionEvent> carried) => Post(() =>
        {
            foreach (var e in carried)
            {
                if (e.Type >= 0)
                {
                    events.Add(e.Type, e.Data, e.CharacterId, e.Inventory);
                }
            }
        });

        private void SendPendingEvents()
        {
            if (!welcomed || connection == null || !connection.IsOpen || events.LastSequence <= eventsSentThrough)
            {
                return;
            }

            var batch = events.After(eventsSentThrough, MaxEventsPerBatch);
            while (batch.Count > 0)
            {
                if (!connection.Send(new EventBatch { Events = batch.ToArray() }))
                {
                    return; // stays in the log, sent after the next Hello
                }
                eventsSentThrough = batch[batch.Count - 1].Sequence;
                batch = events.After(eventsSentThrough, MaxEventsPerBatch);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Commands
        // ---------------------------------------------------------------------------------------------------------

        private void StartCommand(CommandRequest request)
        {
            if (request.RequestId == Guid.Empty)
            {
                connection.Send(new CommandResult { RequestId = request.RequestId, Status = CommandStatus.BadArguments, Error = "A command needs a request id." });
                return;
            }

            if (commands.TryGetValue(request.RequestId, out var existing))
            {
                // a repeat: answer with the first one's result, once it has one
                _ = ReplyWhenDoneAsync(existing.Task);
                return;
            }

            if (!hub.Commands.Knows(request.Command))
            {
                connection.Send(new CommandResult { RequestId = request.RequestId, Status = CommandStatus.UnknownCommand, Error = "Unknown command '" + request.Command + "'." });
                return;
            }

            if (commands.Count >= MaxCommandsInFlight && CountRunning() >= MaxCommandsInFlight)
            {
                connection.Send(new CommandResult { RequestId = request.RequestId, Status = CommandStatus.Busy, Error = "Too many commands at once." });
                return;
            }

            var token = Token;
            // Registered before it starts: a command that fails at once removes its entry (so a retry runs it again),
            // and that removal must not come before the entry is there. Only this worker reads entry.Task.
            var entry = new CommandEntry { Created = DateTime.UtcNow };
            commands[request.RequestId] = entry;
            entry.Task = Task.Run(() => RunCommandAsync(token, request));
            _ = ReplyWhenDoneAsync(entry.Task);
        }

        private int CountRunning()
        {
            var running = 0;
            foreach (var (_, entry) in commands)
            {
                if (!entry.Task.IsCompleted) running++;
            }
            return running;
        }

        private async Task<CommandResult> RunCommandAsync(SessionToken token, CommandRequest request)
        {
            await commandSlots.WaitAsync();
            try
            {
                return await hub.Commands.RunAsync(token, request, hub.NextInventoryRevision);
            }
            catch (CommandFailedException exc)
            {
                logger.LogError("v2 command " + request.Command + " for session " + SessionId + " failed: " + exc.InnerException);
                // not kept: a retry with the same id runs it again
                commands.TryRemove(request.RequestId, out _);
                return exc.Result;
            }
            catch (Exception exc)
            {
                logger.LogError("v2 command " + request.Command + " for session " + SessionId + " failed: " + exc);
                commands.TryRemove(request.RequestId, out _);
                return new CommandResult { RequestId = request.RequestId, Status = CommandStatus.Error, Error = exc.Message };
            }
            finally
            {
                commandSlots.Release();
            }
        }

        private async Task ReplyWhenDoneAsync(Task<CommandResult> task)
        {
            var result = await task;
            // to whichever connection is live now, which may not be the one that asked
            Post(() => connection?.Send(result));
        }
    }
}
