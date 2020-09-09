using Microsoft.EntityFrameworkCore.Diagnostics;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game.Processors.Tasks;
using RavenNest.BusinessLogic.Net;
using RavenNest.Models;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Game.Processors
{
    public class GameProcessor : IGameProcessor
    {
        private const string VillageProcessorName = "Village";

        private readonly ConcurrentDictionary<string, ITaskProcessor> taskProcessors = new ConcurrentDictionary<string, ITaskProcessor>();

        private readonly IGameData gameData;
        private readonly IGameManager gameManager;
        private readonly IIntegrityChecker integrityChecker;
        private readonly IWebSocketConnection ws;
        private readonly ISessionManager sessionManager;
        private readonly IPlayerInventoryProvider inventoryProvider;
        private readonly SessionToken sessionToken;
        private readonly TimeSpan villageInfoPushInterval = TimeSpan.FromSeconds(5);
        private readonly TimeSpan permissionInfoPushInterval = TimeSpan.FromSeconds(60);

        private int gameRevision = 0;
        private DateTime lastVillageInfoPush;
        private DateTime lastPermissionInfoPush;

        public GameProcessor(
            IIntegrityChecker integrityChecker,
            IWebSocketConnection ws,
            ISessionManager sessionManager,
            IPlayerInventoryProvider inventoryProvider,
            IGameData gameData,
            IGameManager gameManager,
            SessionToken sessionToken)
        {
            this.gameData = gameData;
            this.gameManager = gameManager;
            this.integrityChecker = integrityChecker;
            this.ws = ws;
            this.sessionManager = sessionManager;
            this.inventoryProvider = inventoryProvider;
            this.sessionToken = sessionToken;

            RegisterPlayerTask<VillageProcessor>(VillageProcessorName);

            RegisterPlayerTask<FightingTaskProcessor>("Fighting");
            RegisterPlayerTask<MiningTaskProcessor>("Mining");
            RegisterPlayerTask<FishingTaskProcessor>("Fishing");
            RegisterPlayerTask<FarmingTaskProcessor>("Farming");
            RegisterPlayerTask<WoodcuttingTaskProcessor>("Woodcutting");
            RegisterPlayerTask<CraftingTaskProcessor>("Crafting");
            RegisterPlayerTask<CookingTaskProcessor>("Cooking");

            SendSessionData();
        }

        private async void SendSessionData()
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session != null)
            {
                await sessionManager.SendPermissionDataAsync(session);
                sessionManager.SendVillageInfo(session);
            }
        }

        public async Task ProcessAsync(CancellationTokenSource cts)
        {
            UpdateSessionTasks();

            PushVillageInfo();

            await PushGameEventsAsync(cts);

            await PushPermissionDataInfoAsync();
        }

        private void PushVillageInfo()
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session != null)
            {
                var elapsed = DateTime.UtcNow - lastVillageInfoPush;
                if (elapsed >= villageInfoPushInterval)
                {
                    lastVillageInfoPush = DateTime.UtcNow;
                    sessionManager.SendVillageInfo(session);
                }
            }
        }

        private async Task PushPermissionDataInfoAsync()
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session != null)
            {
                var elapsed = DateTime.UtcNow - lastPermissionInfoPush;
                if (elapsed >= permissionInfoPushInterval)
                {
                    lastPermissionInfoPush = DateTime.UtcNow;
                    await sessionManager.SendPermissionDataAsync(session);
                }
            }
        }

        private async Task PushGameEventsAsync(CancellationTokenSource cts)
        {
            var events = gameManager.GetGameEvents(sessionToken);
            if (events.Count > 0)
            {
                var allEvents = events.ToList();
                var batchSize = 10;
                for (var i = 0; i < allEvents.Count;)
                {
                    var eventList = new EventList();
                    eventList.Revision = events.Revision;
                    eventList.Events = allEvents.Skip(batchSize * i).Take(batchSize).ToList();
                    if (await ws.PushAsync("game_event", eventList, cts.Token))
                    {
                        this.gameRevision = events.Revision;
                    }
                    i += allEvents.Count < batchSize ? allEvents.Count : batchSize;
                    await Task.Delay(100);
                }
            }
        }

        private void UpdateSessionTasks()
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            var characters = gameData.GetSessionCharacters(session);
            var villageProcessor = GetTaskProcessor(VillageProcessorName);

            if (session == null)
                return;

            villageProcessor.Handle(integrityChecker, gameData, inventoryProvider, session, null, null);

            foreach (var character in characters)
            {
                var state = gameData.GetState(character.StateId);
                if (state == null)
                {
                    state = new DataModels.CharacterState
                    {
                        Id = Guid.NewGuid()
                    };
                    gameData.Add(state);
                    character.StateId = state.Id;
                }

                if (string.IsNullOrEmpty(state.Task) || state.InArena || state.InRaid || state.Island == "War" || !string.IsNullOrEmpty(state.DuelOpponent))
                {
                    continue;
                }

                ITaskProcessor taskProcessor = GetTaskProcessor(state.Task);
                if (taskProcessor != null)
                    taskProcessor.Handle(integrityChecker, gameData, inventoryProvider, session, character, state);
            }
        }

        private ITaskProcessor GetTaskProcessor(string task)
        {
            if (taskProcessors.TryGetValue(task, out var type))
            {
                return type;
            }

            return null;
        }

        private void RegisterPlayerTask<T>(string taskName) where T : ITaskProcessor, new()
        {
            taskProcessors[taskName] = new T();
        }
    }
}
