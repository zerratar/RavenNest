using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Protocol;

namespace RavenNest.BusinessLogic.Net.V2
{
    /// <summary>
    /// The v2 game connection endpoint (<see cref="ProtocolInfo.Path"/>): accepts WebSockets, checks each Hello,
    /// and hands the connection to its session's <see cref="GameSessionActor"/>. Also routes game events and
    /// database save notifications to the sessions.
    /// </summary>
    public sealed class GameConnectionHub : IDisposable
    {
        private static readonly TimeSpan HelloTimeout = TimeSpan.FromSeconds(10);

        private readonly ConcurrentDictionary<Guid, GameSessionActor> sessionsByUser = new ConcurrentDictionary<Guid, GameSessionActor>();
        private readonly ConcurrentDictionary<Guid, long> inventoryRevisions = new ConcurrentDictionary<Guid, long>();
        private readonly SemaphoreSlim sessionChanges = new SemaphoreSlim(1, 1);
        private readonly GameConnectionTokens tokens;
        private readonly SessionManager sessionManager;
        private readonly ITcpSocketApiConnectionProvider oldConnections;
        private readonly AppSettings settings;
        private readonly ILogger<GameConnectionHub> logger;
        private readonly Timer timer;
        private readonly string eventLogFolder;
        private DateTime lastSweep = DateTime.MinValue;

        public GameConnectionHub(
            GameData gameData,
            PlayerManager playerManager,
            SessionManager sessionManager,
            PlayerInventoryProvider inventories,
            IGameProcessorManager gameProcessors,
            GameConnectionTokens tokens,
            GameEventRouter eventRouter,
            ITcpSocketApiConnectionProvider oldConnections,
            IOptions<AppSettings> settings,
            ILogger<GameConnectionHub> logger)
        {
            Data = gameData;
            Players = playerManager;
            GameProcessors = gameProcessors;
            Commands = new CommandDispatcher(playerManager, inventories);
            this.tokens = tokens;
            this.sessionManager = sessionManager;
            this.oldConnections = oldConnections;
            this.settings = settings.Value;
            this.logger = logger;
            eventLogFolder = Path.Combine(FolderPaths.GeneratedDataPath, "v2-session-events");

            eventRouter.Handler = RouteEvent;
            gameData.SaveCompleted += OnSaveCompleted;
            timer = new Timer(_ => OnTick(), null, 1000, 1000);
        }

        internal GameData Data { get; }
        internal PlayerManager Players { get; }
        internal IGameProcessorManager GameProcessors { get; }
        internal CommandDispatcher Commands { get; }

        public int SessionCount => sessionsByUser.Count;

        /// <summary>v2 is on unless AppSettings:DisableGameConnectionV2 is set; old clients never see the difference.</summary>
        public bool Enabled => !settings.DisableGameConnectionV2;

        /// <summary>The next revision of a character's inventory, for changes a command or event carries.</summary>
        internal long NextInventoryRevision(Guid characterId)
        {
            return inventoryRevisions.AddOrUpdate(characterId, 1, (_, r) => r + 1);
        }

        public async Task HandleAsync(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var connection = new GameConnection(socket, context.Connection.RemoteIpAddress?.ToString(), logger);

            if (!Enabled)
            {
                connection.Reject(new Rejected { Reason = RejectReason.Disabled, Message = "The v2 connection is switched off; use the old one." });
                await connection.RunAsync((c, m) => Task.CompletedTask, context.RequestAborted);
                return;
            }

            GameSessionActor session = null;
            _ = Task.Delay(HelloTimeout).ContinueWith(_ =>
            {
                if (session == null && connection.IsOpen)
                {
                    connection.Reject(new Rejected { Reason = RejectReason.BadMessage, Message = "No Hello." });
                }
            });

            await connection.RunAsync(async (c, message) =>
            {
                if (session != null)
                {
                    session.Receive(c, message);
                    return;
                }

                if (message is Hello hello)
                {
                    session = await SayHelloAsync(c, hello);
                }
                else
                {
                    c.Reject(new Rejected { Reason = RejectReason.BadMessage, Message = "The first message must be Hello." });
                }
            }, context.RequestAborted);

            session?.Detach(connection);
        }

        private async Task<GameSessionActor> SayHelloAsync(GameConnection connection, Hello hello)
        {
            if (hello.ProtocolVersion != ProtocolInfo.Version)
            {
                connection.Reject(new Rejected { Reason = RejectReason.ProtocolMismatch, Message = "This server speaks protocol " + ProtocolInfo.Version + "." });
                return null;
            }

            if (!tokens.TryValidate(hello.Token, hello.ClientVersion, out var token, out var error))
            {
                logger.LogWarning("v2 Hello from " + connection.Remote + " refused: " + error);
                connection.Reject(new Rejected { Reason = RejectReason.InvalidToken, Message = "The session token was not accepted (" + error + "). Begin a new session." });
                return null;
            }

            if (!sessionManager.IsExpectedVersion(hello.ClientVersion))
            {
                connection.Reject(new Rejected
                {
                    Reason = RejectReason.ClientTooOld,
                    Message = "Please update the game.",
                    ExpectedClientVersion = Data.Client?.ClientVersion
                });
                return null;
            }

            var session = await GetOrStartSessionAsync(token);
            session.Attach(connection, hello, token);
            return session;
        }

        /// <summary>
        /// The streamer's actor for this game session. A streamer has one at a time: a newer game session replaces
        /// the older one's actor, and the events it never delivered carry over.
        /// </summary>
        private async Task<GameSessionActor> GetOrStartSessionAsync(RavenNest.Models.SessionToken token)
        {
            await sessionChanges.WaitAsync();
            try
            {
                if (sessionsByUser.TryGetValue(token.UserId, out var existing))
                {
                    if (existing.SessionId == token.SessionId && !existing.Ended)
                    {
                        return existing;
                    }

                    var carried = await existing.EndAsync(deleteLog: true);
                    var replacement = Start(token);
                    if (carried.Count > 0)
                    {
                        replacement.AddEvents(carried);
                    }
                    return replacement;
                }

                return Start(token);
            }
            finally
            {
                sessionChanges.Release();
            }
        }

        private GameSessionActor Start(RavenNest.Models.SessionToken token)
        {
            var log = new SessionEventLog(eventLogFolder, token.SessionId);
            var session = new GameSessionActor(this, token, log, logger);
            sessionsByUser[token.UserId] = session;

            // events that waited on the old path while the streamer had no v2 session
            var pending = oldConnections.GetPendingEvents(token.UserId);
            var waiting = pending.Peek(PendingGameEvents.MaxEvents, out var last);
            if (waiting.Count > 0)
            {
                pending.RemoveThrough(last);
                session.AddEvents(waiting.Select(e => new SessionEvent { Type = e.Type, Data = e.Data }).ToList());
            }

            logger.LogInformation("v2 session started for " + token.UserName + " (" + token.SessionId + ")" + (log.Count > 0 ? ", " + log.Count + " events from its log" : ""));
            return session;
        }

        internal void Remove(GameSessionActor session)
        {
            sessionsByUser.TryRemove(new System.Collections.Generic.KeyValuePair<Guid, GameSessionActor>(session.UserId, session));
        }

        private bool RouteEvent(DataModels.GameEvent gameEvent)
        {
            if (gameEvent == null || !sessionsByUser.TryGetValue(gameEvent.UserId, out var session) || session.Ended)
            {
                return false;
            }

            session.AddEvent(gameEvent.Type, gameEvent.Data);
            return true;
        }

        private void OnSaveCompleted(DateTime started, bool clean)
        {
            foreach (var session in sessionsByUser.Values)
            {
                session.SaveCompleted(started, clean);
            }
        }

        private void OnTick()
        {
            try
            {
                foreach (var session in sessionsByUser.Values)
                {
                    session.Tick();
                }

                if (DateTime.UtcNow - lastSweep > TimeSpan.FromHours(6))
                {
                    lastSweep = DateTime.UtcNow;
                    var live = sessionsByUser.Values.Select(x => x.SessionId.ToString("N")).ToHashSet();
                    var removed = SessionEventLog.Sweep(eventLogFolder, TimeSpan.FromDays(1), live);
                    if (removed > 0)
                    {
                        logger.LogInformation("Removed " + removed + " v2 event logs nobody came back for.");
                    }
                }
            }
            catch (Exception exc)
            {
                logger.LogError("v2 hub tick failed: " + exc);
            }
        }

        public void Dispose()
        {
            timer.Dispose();
            Data.SaveCompleted -= OnSaveCompleted;
        }
    }
}
