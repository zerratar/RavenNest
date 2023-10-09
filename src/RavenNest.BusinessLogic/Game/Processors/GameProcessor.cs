using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game.Processors.Tasks;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Providers;
using RavenNest.BusinessLogic.Twitch.Extension;
using RavenNest.DataModels;
using RavenNest.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private const string RestedProcessorName = "Rested";

        private readonly ConcurrentDictionary<string, ITaskProcessor> taskProcessors
            = new ConcurrentDictionary<string, ITaskProcessor>();

        private readonly ConcurrentDictionary<Guid, double> reportedCoinsAmount
            = new ConcurrentDictionary<Guid, double>();

        private readonly GameData gameData;
        private readonly ILogger logger;
        private readonly IRavenBotApiClient ravenbotApi;
        private readonly ITwitchExtensionConnectionProvider extensionConnectionProvider;
        private readonly ITcpSocketApiConnectionProvider tcpConnectionProvider;
        private readonly SessionManager sessionManager;
        private readonly PlayerInventoryProvider inventoryProvider;
        private readonly SessionToken sessionToken;

        private readonly DateTime activatedUtc;
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
            ILogger logger,
            IRavenBotApiClient ravenbotApi,
            ITwitchExtensionConnectionProvider extWsProvider,
            ITcpSocketApiConnectionProvider tcpConnectionProvider,
            SessionManager sessionManager,
            PlayerInventoryProvider inventoryProvider,
            GameData gameData,
            SessionToken sessionToken)
        {
            this.gameData = gameData;
            this.logger = logger;
            this.ravenbotApi = ravenbotApi;
            this.extensionConnectionProvider = extWsProvider;
            this.tcpConnectionProvider = tcpConnectionProvider;
            this.sessionManager = sessionManager;
            this.inventoryProvider = inventoryProvider;
            this.sessionToken = sessionToken;
            this.activatedUtc = DateTime.UtcNow;

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
                sessionManager.SendSessionSettings(session);
                sessionManager.SendVillageInfo(session);
                sessionManager.SendExpMultiplier(session);
                ravenbotApi.UpdateUserSettingsAsync(session.UserId);
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
                lastVersionUpdateSent = DateTime.UnixEpoch;
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
                        await ravenbotApi.UpdateUserSettingsAsync(user.Id);
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
                    sessionManager.SendSessionSettings(session);
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

        protected void UpdateResources(
            GameData gameData,
            DataModels.GameSession session,
            DataModels.Character character,
            DataModels.Resources resources)
        {
            var gameEvent = gameData.CreateSessionEvent(GameEventType.ResourceUpdate, session,
                new ResourceUpdate
                {
                    CharacterId = character.Id,
                    //FishAmount = resources.Fish,
                    //OreAmount = resources.Ore,
                    //WheatAmount = resources.Wheat,
                    //WoodAmount = resources.Wood,
                    CoinsAmount = resources.Coins
                });

            gameData.EnqueueGameEvent(gameEvent);
        }

        private void SyncCharacterResources(DataModels.GameSession session, Character character)
        {
            var resources = gameData.GetResources(character);
            var exists = reportedCoinsAmount.TryGetValue(character.Id, out var val);
            if (val != resources.Coins)
            {
                reportedCoinsAmount[character.Id] = resources.Coins;

                // only send the update if it already exists, this makes sure that players that join in late dont get spammed these messages.
                if (exists)
                {
                    UpdateResources(gameData, session, character, resources);
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
                village.Process(logger, gameData, inventoryProvider, session, null, null);

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

                    SyncCharacterResources(session, character);

                    UpdateActiveStatusEffects(utcNow, character);

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

                    rested.Process(logger, gameData, inventoryProvider, session, character, state);
                    clan.Process(logger, gameData, inventoryProvider, session, character, state);
                    loyalty.Process(logger, gameData, inventoryProvider, session, character, state);

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
                        task.Process(logger, gameData, inventoryProvider, session, character, state);
                }
            }
        }

        private void UpdateActiveStatusEffects(DateTime utcNow, Character character)
        {
            var effects = gameData.GetCharacterStatusEffects(character.Id).ToList();
            if (effects.Count > 0)
            {
                foreach (var effect in effects)
                {
                    if (effect.LastUpdateUtc < activatedUtc)
                    {
                        effect.LastUpdateUtc = DateTime.UtcNow;
                    }

                    var elapsed = utcNow - effect.LastUpdateUtc;

                    if (effect.ExpiresUtc > utcNow && (effect.Duration == 0 || effect.TimeLeft == 0))
                    {
                        effect.TimeLeft = (utcNow - effect.ExpiresUtc).TotalSeconds;
                        effect.Duration = (effect.ExpiresUtc - effect.StartUtc).TotalSeconds;
                    }
                    else
                    {
                        effect.TimeLeft -= elapsed.TotalSeconds;
                    }

                    effect.LastUpdateUtc = utcNow;

                    if (effect.TimeLeft <= 0)//if (utcNow >= effect.ExpiresUtc)
                    {
                        gameData.Remove(effect);
                    }
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
