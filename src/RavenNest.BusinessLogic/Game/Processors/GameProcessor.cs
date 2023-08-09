using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game.Processors.Tasks;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Providers;
using RavenNest.BusinessLogic.Twitch.Extension;
using RavenNest.Models;
using System;
using System.Collections.Concurrent;
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

        private readonly ConcurrentDictionary<string, ITaskProcessor> taskProcessors
            = new ConcurrentDictionary<string, ITaskProcessor>();

        private readonly GameData gameData;
        private readonly IRavenBotApiClient ravenbotApi;
        private readonly ITwitchExtensionConnectionProvider extensionConnectionProvider;
        private readonly ITcpSocketApiConnectionProvider tcpConnectionProvider;
        private readonly SessionManager sessionManager;
        private readonly PlayerInventoryProvider inventoryProvider;
        private readonly SessionToken sessionToken;

        private readonly TimeSpan ExpMultiplierPushInterval = TimeSpan.FromSeconds(15);
        private readonly TimeSpan villageInfoPushInterval = TimeSpan.FromSeconds(30);
        private readonly TimeSpan pubsubPushInterval = TimeSpan.FromSeconds(30);
        private readonly TimeSpan permissionInfoPushInterval = TimeSpan.FromSeconds(10);

        private DateTime lastVillageInfoPush;
        private DateTime lastPermissionInfoPush;
        private DateTime lastExpMultiPush;
        private DateTime lastPubsubPush;
        private DateTime lastVersionUpdateSent;

        public GameProcessor(
            IRavenBotApiClient ravenbotApi,
            ITwitchExtensionConnectionProvider extWsProvider,
            ITcpSocketApiConnectionProvider tcpConnectionProvider,
            SessionManager sessionManager,
            PlayerInventoryProvider inventoryProvider,
            GameData gameData,
            SessionToken sessionToken)
        {
            this.gameData = gameData;
            this.ravenbotApi = ravenbotApi;
            this.extensionConnectionProvider = extWsProvider;
            this.tcpConnectionProvider = tcpConnectionProvider;
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
            RegisterPlayerTask<GatheringTaskProcessor>("Gathering");
            RegisterPlayerTask<AlchemyTaskProcessor>("Alchemy");
            RegisterPlayerTask<FarmingTaskProcessor>("Farming");
            RegisterPlayerTask<SailingTaskProcessor>("Sailing");
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
                ravenbotApi.UpdateUserSettings(session.UserId);
            }
        }

        public void Process(CancellationTokenSource cts)
        {
            var now = DateTime.UtcNow;

            UpdateSessionTasks(now);

            PushVillageInfo(now);

            PushExpMultiplier(now);

            //PushServerTime();

            PushGameUpdated(now);

            PushPermissionDataInfo(now);

            PushGameEvents();

            PushPubSubDetailsAsync(now);
        }

        private void PushGameUpdated(DateTime now)
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session == null)
            {
                return;
            }
            var updateRequiredKey = "sent_update_required";
            var state = gameData.GetSessionState(session.Id);
            if (gameData.IsExpectedVersion(state.ClientVersion))
            {
                lastVersionUpdateSent = DateTime.MinValue;
                state[updateRequiredKey] = false;
                return;
            }

            var value = state.GetOrDefault<bool>(updateRequiredKey);
            if (value || (now - lastVersionUpdateSent >= TimeSpan.FromSeconds(5)))
            {
                var version = gameData.Client.ClientVersion;
                var gameEvent = gameData.CreateSessionEvent(GameEventType.GameUpdated, session, new GameUpdatedRequest
                {
                    ExpectedVersion = version,
                    UpdateRequired = GameUpdates.IsRequiredUpdate(version)
                });

                gameData.EnqueueGameEvent(gameEvent);
            }

            lastVersionUpdateSent = now;
            state[updateRequiredKey] = true;
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
                        ravenbotApi.UpdateUserSettings(user.Id);
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

        private void PushGameEvents()
        {
            if (tcpConnectionProvider.TryGet(this.sessionToken.SessionId, out var connection) && connection.Connected)
            {
                connection.ProcessSendQueue();
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
                village.Process(gameData, inventoryProvider, session, null, null);

            var characters = gameData.GetActiveSessionCharacters(session);
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

                    rested.Process(gameData, inventoryProvider, session, character, state);
                    clan.Process(gameData, inventoryProvider, session, character, state);
                    loyalty.Process(gameData, inventoryProvider, session, character, state);

                    if (string.IsNullOrEmpty(state.Task)
                        || (state.InOnsen ?? false)
                        || (state.InDungeon ?? false)
                        || state.InArena
                        || state.InRaid
                        || state.Island == "War")
                    {
                        continue;
                    }

                    var taskName = state.Task;
                    if (string.IsNullOrEmpty(state.Island))
                    {
                        taskName = "Sailing";
                    }

                    var task = GetTaskProcessor(taskName);
                    if (task != null)
                        task.Process(gameData, inventoryProvider, session, character, state);
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
            taskProcessors[taskName].SetTcpSocketApiConnectionProvider(tcpConnectionProvider);
        }
    }
}
