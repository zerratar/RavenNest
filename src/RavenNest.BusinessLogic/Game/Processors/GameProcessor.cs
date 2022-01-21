using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game.Processors.Tasks;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Providers;
using RavenNest.BusinessLogic.Twitch.Extension;
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
        private const string LoyaltyProcessorName = "loyalty";
        private const string ClanProcessorName = "Clan";
        private const string RestedProcessorName = "Rested";

        private readonly ConcurrentDictionary<string, ITaskProcessor> taskProcessors = new ConcurrentDictionary<string, ITaskProcessor>();

        private readonly IGameData gameData;
        private readonly IGameManager gameManager;
        private readonly IRavenBotApiClient ravenbotApi;
        private readonly IIntegrityChecker integrityChecker;
        private readonly IGameWebSocketConnection gameConnection;
        private readonly IExtensionWebSocketConnectionProvider extensionConnectionProvider;
        private readonly ISessionManager sessionManager;
        private readonly IPlayerInventoryProvider inventoryProvider;
        private readonly SessionToken sessionToken;

        private readonly TimeSpan ServerTimePushInterval = TimeSpan.FromSeconds(3);
        private readonly TimeSpan ExpMultiplierPushInterval = TimeSpan.FromSeconds(3);
        private readonly TimeSpan villageInfoPushInterval = TimeSpan.FromSeconds(10);
        private readonly TimeSpan pubsubPushInterval = TimeSpan.FromSeconds(30);
        private readonly TimeSpan permissionInfoPushInterval = TimeSpan.FromSeconds(60);

        private DateTime lastVillageInfoPush;
        private DateTime lastPermissionInfoPush;
        private DateTime lastExpMultiPush;
        private DateTime lastServerTimePush;
        private DateTime lastPubsubPush;

        public GameProcessor(
            IRavenBotApiClient ravenbotApi,
            IIntegrityChecker integrityChecker,
            IGameWebSocketConnection websocket,
            IExtensionWebSocketConnectionProvider extWsProvider,
            ISessionManager sessionManager,
            IPlayerInventoryProvider inventoryProvider,
            IGameData gameData,
            IGameManager gameManager,
            SessionToken sessionToken)
        {
            this.gameData = gameData;
            this.gameManager = gameManager;
            this.ravenbotApi = ravenbotApi;
            this.integrityChecker = integrityChecker;
            this.gameConnection = websocket;
            this.extensionConnectionProvider = extWsProvider;
            this.sessionManager = sessionManager;
            this.inventoryProvider = inventoryProvider;
            this.sessionToken = sessionToken;

            RegisterPlayerTask<ClanProcessor>(ClanProcessorName);
            RegisterPlayerTask<VillageProcessor>(VillageProcessorName);
            RegisterPlayerTask<LoyaltyProcessor>(LoyaltyProcessorName);
            RegisterPlayerTask<RestedProcessor>(RestedProcessorName);
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
            var now = DateTime.UtcNow;

            UpdateSessionTasks(now);

            PushVillageInfo(now);

            PushExpMultiplier(now);

            //PushServerTime();

            await PushGameEventsAsync(now, cts);

            PushPermissionDataInfo(now);

            await PushPubSubDetailsAsync(now);
        }

        private async Task PushPubSubDetailsAsync(DateTime utcNow)
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session != null)
            {
                var user = gameData.GetUser(session.UserId);
                var elapsed = utcNow - lastPubsubPush;
                if (elapsed >= pubsubPushInterval && user != null)
                {
                    var accessToken = gameData.GetUserProperty(session.UserId, UserProperties.Twitch_PubSub);
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        await ravenbotApi.SendPubSubAccessTokenAsync(user.UserId, user.UserName, accessToken);

                        sessionManager.SendPubSubToken(session, accessToken);
                    }
                    lastPubsubPush = utcNow;
                }
            }
        }

        private void PushVillageInfo(DateTime utcNow)
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session != null)
            {
                var elapsed = utcNow - lastVillageInfoPush;
                if (elapsed >= villageInfoPushInterval)
                {
                    lastVillageInfoPush = utcNow;
                    sessionManager.SendVillageInfo(session);
                }
            }
        }

        private void PushExpMultiplier(DateTime utcNow)
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session != null)
            {
                var elapsed = utcNow - lastExpMultiPush;
                if (elapsed >= ExpMultiplierPushInterval)
                {
                    lastExpMultiPush = utcNow;
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

        private void PushPermissionDataInfo(DateTime utcNow)
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session != null)
            {
                var elapsed = utcNow - lastPermissionInfoPush;
                if (elapsed >= permissionInfoPushInterval)
                {
                    lastPermissionInfoPush = utcNow;
                    sessionManager.SendPermissionData(session);
                }
            }
        }

        private async Task PushGameEventsAsync(DateTime utcNow, CancellationTokenSource cts)
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
                    await gameConnection.PushAsync("game_event", eventList, cts.Token);
                    i += allEvents.Count < batchSize ? allEvents.Count : batchSize;
                    //await Task.Delay(50);
                }
            }
        }

        private void UpdateSessionTasks(DateTime utcNow)
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session == null)
                return;


            // force keep a session alive if we are connected here
            session.Stopped = null;
            session.Status = (int)SessionStatus.Active;
            session.Updated = utcNow;


            var village = GetTaskProcessor(VillageProcessorName);
            if (village != null)
                village.Process(integrityChecker, gameData, inventoryProvider, session, null, null);


            var characters = gameData.GetSessionCharacters(session);
            if (characters.Count > 0)
            {
                var loyalty = GetTaskProcessor(LoyaltyProcessorName);
                var clan = GetTaskProcessor(ClanProcessorName);
                var rested = GetTaskProcessor(RestedProcessorName);

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

                    rested.Process(integrityChecker, gameData, inventoryProvider, session, character, state);
                    clan.Process(integrityChecker, gameData, inventoryProvider, session, character, state);
                    loyalty.Process(integrityChecker, gameData, inventoryProvider, session, character, state);

                    if (string.IsNullOrEmpty(state.Task)
                        || (state.InOnsen ?? false)
                        || (state.InDungeon ?? false)
                        || state.InArena
                        || state.InRaid
                        || string.IsNullOrEmpty(state.Island)
                        || state.Island == "War"
                        || !string.IsNullOrEmpty(state.DuelOpponent))
                    {
                        continue;
                    }

                    var task = GetTaskProcessor(state.Task);
                    if (task != null)
                        task.Process(integrityChecker, gameData, inventoryProvider, session, character, state);
                }
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
            taskProcessors[taskName].SetExtensionConnectionProvider(extensionConnectionProvider);
        }
    }
}
