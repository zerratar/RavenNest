using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game.Processors.Tasks;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Providers;
using RavenNest.Models;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Game.Processors
{
    public class GameProcessor : IGameProcessor
    {
        private const string VillageProcessorName = "Village";
        private const string LoyaltyProcessorName = "loyalty";
        private const string ClanProcessorName = "Clan";

        private readonly ConcurrentDictionary<string, ITaskProcessor> taskProcessors = new ConcurrentDictionary<string, ITaskProcessor>();

        private readonly IGameData gameData;
        private readonly IGameManager gameManager;
        private readonly IIntegrityChecker integrityChecker;
        private readonly IWebSocketConnection ws;
        private readonly ISessionManager sessionManager;
        private readonly IPlayerInventoryProvider inventoryProvider;
        private readonly SessionToken sessionToken;

        private readonly TimeSpan ServerTimePushInterval = TimeSpan.FromSeconds(3);
        private readonly TimeSpan ExpMultiplierPushInterval = TimeSpan.FromSeconds(3);
        private readonly TimeSpan villageInfoPushInterval = TimeSpan.FromSeconds(2);
        private readonly TimeSpan permissionInfoPushInterval = TimeSpan.FromSeconds(60);

        private DateTime lastVillageInfoPush;
        private DateTime lastPermissionInfoPush;
        private DateTime lastExpMultiPush;
        private DateTime lastServerTimePush;

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

            RegisterPlayerTask<ClanProcessor>(ClanProcessorName);
            RegisterPlayerTask<VillageProcessor>(VillageProcessorName);
            RegisterPlayerTask<LoyaltyProcessor>(LoyaltyProcessorName);
            RegisterPlayerTask<FightingTaskProcessor>("Fighting");
            RegisterPlayerTask<MiningTaskProcessor>("Mining");
            RegisterPlayerTask<FishingTaskProcessor>("Fishing");
            RegisterPlayerTask<FarmingTaskProcessor>("Farming");
            RegisterPlayerTask<WoodcuttingTaskProcessor>("Woodcutting");
            RegisterPlayerTask<CraftingTaskProcessor>("Crafting");
            RegisterPlayerTask<CookingTaskProcessor>("Cooking");

            SendSessionData();
        }

        private void SendSessionData()
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session != null)
            {
                sessionManager.SendPermissionData(session);
                sessionManager.SendVillageInfo(session);
                sessionManager.SendExpMultiplier(session);
            }
        }

        public async Task ProcessAsync(CancellationTokenSource cts)
        {
            UpdateSessionTasks();

            PushVillageInfo();

            PushExpMultiplier();

            //PushServerTime();

            await PushGameEventsAsync(cts);

            PushPermissionDataInfo();
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

        private void PushExpMultiplier()
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session != null)
            {
                var elapsed = DateTime.UtcNow - lastExpMultiPush;
                if (elapsed >= ExpMultiplierPushInterval)
                {
                    lastExpMultiPush = DateTime.UtcNow;
                    sessionManager.SendExpMultiplier(session);
                }
            }
        }

        private void PushServerTime()
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session != null)
            {
                var elapsed = DateTime.UtcNow - lastServerTimePush;
                if (elapsed >= ServerTimePushInterval)
                {
                    lastServerTimePush = DateTime.UtcNow;
                    sessionManager.SendServerTime(session);
                }
            }
        }

        private void PushPermissionDataInfo()
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session != null)
            {
                var elapsed = DateTime.UtcNow - lastPermissionInfoPush;
                if (elapsed >= permissionInfoPushInterval)
                {
                    lastPermissionInfoPush = DateTime.UtcNow;
                    sessionManager.SendPermissionData(session);
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
                    await ws.PushAsync("game_event", eventList, cts.Token);
                    i += allEvents.Count < batchSize ? allEvents.Count : batchSize;
                    await Task.Delay(50);
                }
            }
        }

        private void UpdateSessionTasks()
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            var characters = gameData.GetSessionCharacters(session);
            var villageProcessor = GetTaskProcessor(VillageProcessorName);
            var loyaltyProcessor = GetTaskProcessor(LoyaltyProcessorName);
            var clanProcessor = GetTaskProcessor(ClanProcessorName);

            if (session == null)
                return;

            // force keep a session alive if we are connected here
            session.Stopped = null;
            session.Status = (int)SessionStatus.Active;

            villageProcessor.Handle(integrityChecker, gameData, inventoryProvider, session, null, null);

            foreach (var character in characters)
            {
                //if (character.CharacterIndex == 0)
                //{
                //    var inventory = inventoryProvider.Get(character.Id);
                //    inventory.AddPatreonTierRewards();
                //}

                var state = gameData.GetCharacterState(character.StateId);
                if (state == null)
                {
                    state = new DataModels.CharacterState
                    {
                        Id = Guid.NewGuid()
                    };
                    gameData.Add(state);
                    character.StateId = state.Id;
                }

                clanProcessor.Handle(integrityChecker, gameData, inventoryProvider, session, character, state);
                loyaltyProcessor.Handle(integrityChecker, gameData, inventoryProvider, session, character, state);

                if (string.IsNullOrEmpty(state.Task) || (state.InDungeon ?? false) || state.InArena || state.InRaid || state.Island == "War" || !string.IsNullOrEmpty(state.DuelOpponent))
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
