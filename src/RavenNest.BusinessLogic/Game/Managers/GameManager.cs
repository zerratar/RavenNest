using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using RavenNest.Models;
using System;
using System.Linq;

namespace RavenNest.BusinessLogic.Game
{
    public class GameManager : IGameManager
    {
        private readonly IServerManager serverManager;
        private readonly ISessionManager sessionManager;
        private readonly IPlayerInventoryProvider inventoryProvider;
        private readonly IGameData gameData;

        private Guid expScrollId;
        private Guid dungeonScrollId;
        private Guid raidScrollId;

        public GameManager(
            IServerManager serverManager,
            ISessionManager sessionManager,
            IPlayerInventoryProvider inventoryProvider,
            IGameData gameData)
        {
            this.serverManager = serverManager;
            this.sessionManager = sessionManager;
            this.inventoryProvider = inventoryProvider;
            this.gameData = gameData;

            var items = gameData.GetItems();

            this.raidScrollId = items.FirstOrDefault(x => x.Name.ToLower() == "raid scroll").Id;
            this.dungeonScrollId = items.FirstOrDefault(x => x.Name.ToLower() == "dungeon scroll").Id;
            this.expScrollId = items.FirstOrDefault(x => x.Name.ToLower().Contains("exp ") && x.Name.ToLower().Contains(" scroll")).Id;
        }

        public GameInfo GetGameInfo(SessionToken session)
        {
            return null;
        }

        public UseExpScrollResult UseExpScroll(SessionToken sessionToken, Guid characterId, int count)
        {
            if (count <= 0)
                return UseExpScrollResult.Error(sessionManager.GetExpMultiplier());

            var session = gameData.GetSession(sessionToken.SessionId);
            if (session == null)
                return UseExpScrollResult.Error(sessionManager.GetExpMultiplier());

            var character = gameData.GetCharacter(characterId);
            var inventory = inventoryProvider.Get(characterId);

            DataModels.UserBankItem bankItemScroll = null;

            var scrolls = inventory.GetUnequippedItems(DataModels.ItemCategory.Scroll);
            var scroll = scrolls.FirstOrDefault(x => x.Item.Name.Contains("exp", StringComparison.OrdinalIgnoreCase));
            if (scroll.IsNull() || scroll.Amount <= 0)
            {
                var bankItems = gameData.GetUserBankItems(character.UserId);
                bankItemScroll = bankItems.FirstOrDefault(x => IsScrollOfType(x, ScrollType.Experience));
                if (bankItemScroll == null)
                {
                    return UseExpScrollResult.InsufficientScrolls(sessionManager.GetExpMultiplier());
                }
            }

            int left = serverManager.GetIncreasableGlobalExpAmount();
            int usageCount = count;

            if (bankItemScroll != null)
            {
                usageCount = (int)bankItemScroll.Amount;
            }
            else
            {
                usageCount = (int)scroll.Amount;
            }

            usageCount = (int)Math.Min(count, Math.Min(usageCount, left));
            if (left <= 0 || usageCount <= 0)
            {
                return UseExpScrollResult.Success(0, sessionManager.GetExpMultiplier());
            }

            var user = gameData.GetUser(character.UserId);
            if (serverManager.IncreaseGlobalExpMultiplier(user, usageCount))
            {
                if (bankItemScroll != null)
                {
                    gameData.RemoveFromStash(bankItemScroll, usageCount);
                }
                else
                {
                    inventory.RemoveItem(scroll, usageCount);
                }

                return UseExpScrollResult.Success(usageCount, sessionManager.GetExpMultiplier());
            }

            return UseExpScrollResult.Success(0, sessionManager.GetExpMultiplier());
        }

        public ScrollUseResult UseScroll(SessionToken sessionToken, Guid characterId, ScrollType scrollType)
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session == null)
                return ScrollUseResult.Error;

            var character = gameData.GetCharacter(characterId);
            //if (session.UserId != character.UserIdLock)
            //    return ScrollUseResult.Error;

            var inventory = inventoryProvider.Get(characterId);
            var scrolls = inventory.GetUnequippedItems(DataModels.ItemCategory.Scroll);


            DataModels.UserBankItem bankItemScroll = null;
            var scroll = scrolls.FirstOrDefault(x => IsScrollOfType(x, scrollType));
            if (scroll.IsNull())
            {
                var bankItems = gameData.GetUserBankItems(character.UserId);
                bankItemScroll = bankItems.FirstOrDefault(x => IsScrollOfType(x, scrollType));
                if (bankItemScroll == null)
                {
                    return ScrollUseResult.InsufficientScrolls;
                }
            }
            var result = ScrollUseResult.Error;
            var isExpScroll = scrollType == ScrollType.Experience;//scroll.Item.Name.Contains("exp", StringComparison.OrdinalIgnoreCase);
            if (isExpScroll)
            {
                if (!serverManager.CanIncreaseGlobalExpMultiplier())
                    return ScrollUseResult.Error;

                var user = gameData.GetUser(character.UserId);
                if (serverManager.IncreaseGlobalExpMultiplier(user))
                {
                    if (bankItemScroll != null)
                    {
                        // Remove Scroll from Stash
                        gameData.RemoveFromStash(bankItemScroll, 1);
                    }
                    else
                    {
                        inventory.RemoveItem(scroll, 1);
                    }

                    return ScrollUseResult.Success;
                }
            }
            else
            {
                if (bankItemScroll != null)
                {
                    // Remove Scroll from Stash
                    if (gameData.RemoveFromStash(bankItemScroll, 1))
                    {
                        return ScrollUseResult.Success;
                    }
                }
                else
                {
                    if (inventory.RemoveItem(scroll, 1))
                    {
                        return ScrollUseResult.Success;
                    }
                }
            }

            return ScrollUseResult.Error;
        }

        private bool IsScrollOfType(DataModels.UserBankItem item, ScrollType scrollType)
        {
            if (scrollType == ScrollType.Experience)
            {
                return item.ItemId == expScrollId;
            }

            if (scrollType == ScrollType.Raid)
            {
                return item.ItemId == raidScrollId;
            }

            return item.ItemId == dungeonScrollId;
        }

        private bool IsScrollOfType(ReadOnlyInventoryItem item, ScrollType scrollType)
        {
            return item.Item.Name.Contains(scrollType == ScrollType.Experience ? "exp" : scrollType.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public EventCollection GetGameEvents(SessionToken session)
        {
            var gameSession = gameData.GetSession(session.SessionId);
            if (gameSession == null)
            {
                return new EventCollection();
            }

            var events = gameData.GetSessionEvents(gameSession).AsList();
            if (events == null || events.Count == 0)
                events = gameData.GetUserEvents(gameSession.UserId).AsList();

            var eventCollection = new EventCollection();

            foreach (var ev in events)
            {
                var gameEvent = ModelMapper.Map(ev);
                if (eventCollection.Revision < gameEvent.Revision)
                    eventCollection.Revision = gameEvent.Revision;

                //if (gameEvent.Revision > gameSession.Revision)
                eventCollection.Add(gameEvent);
                gameData.Remove(ev);
            }

            //if (eventCollection.Revision > gameSession.Revision)
            //{
            //    gameSession.Revision = eventCollection.Revision;
            //}

            return eventCollection;
        }

        public ScrollInfoCollection GetScrolls(SessionToken session, Guid characterId)
        {
            var gameSession = gameData.GetSession(session.SessionId);
            if (gameSession == null)
            {
                return new ScrollInfoCollection();
            }

            var inventory = inventoryProvider.Get(characterId);
            if (inventory == null)
            {
                return new ScrollInfoCollection();
            }

            var scrolls = inventory.GetUnequippedItems(DataModels.ItemCategory.Scroll);
            if (scrolls.Count == 0)
            {
                return new ScrollInfoCollection();
            }

            return new ScrollInfoCollection(scrolls.Select(x => new ScrollInfo(x.ItemId, x.Item?.Name, x.Amount)));
        }

        //public bool WalkTo(string userId, int x, int y, int z)
        //{
        //    var targetSession = gameData.GetSessionByUserId(userId);
        //    if (targetSession == null) return false;

        //    var character = gameData.GetCharacterByUserId(userId);
        //    if (character == null) return false;

        //    gameData.EnqueueGameEvent(gameData.CreateSessionEvent(
        //        GameEventType.PlayerMove,
        //        targetSession,
        //        new PlayerMove()
        //        {
        //            UserId = userId,
        //            X = x,
        //            Y = y,
        //            Z = z
        //        }
        //    ));

        //    return true;
        //}

        //public bool Attack(string userId, string targetId, AttackType attackType)
        //{
        //    var targetSession = gameData.GetSessionByUserId(userId);
        //    if (targetSession == null) return false;

        //    var character = gameData.GetCharacterByUserId(userId);
        //    if (character == null) return false;

        //    gameData.EnqueueGameEvent(gameData.CreateSessionEvent(
        //        GameEventType.PlayerAttack,
        //        targetSession,
        //        new PlayerAttack()
        //        {
        //            UserId = userId,
        //            TargetId = targetId,
        //            AttackType = (int)attackType,
        //        }
        //    ));

        //    return true;
        //}

        //public bool ObjectAction(string userId, string targetId, ObjectActionType actionType)
        //{
        //    var targetSession = gameData.GetSessionByUserId(userId);
        //    if (targetSession == null) return false;

        //    var character = gameData.GetCharacterByUserId(userId);
        //    if (character == null) return false;

        //    gameData.EnqueueGameEvent(gameData.CreateSessionEvent(
        //        GameEventType.PlayerAction,
        //        targetSession,
        //        new PlayerAction()
        //        {
        //            UserId = userId,
        //            TargetId = targetId,
        //            ActionType = (int)actionType,
        //        }
        //    ));

        //    return true;
        //}

        //public bool Join(string userId, string targetUserId)
        //{
        //    var targetSession = gameData.GetSessionByUserId(targetUserId);
        //    if (targetSession == null) return false;

        //    var character = gameData.GetCharacterByUserId(userId);
        //    if (character == null) return false;

        //    var state = gameData.GetState(character.StateId);

        //    // just push the event to the client
        //    // and make the client to try and add the player
        //    gameData.EnqueueGameEvent(gameData.CreateSessionEvent(
        //        GameEventType.PlayerAdd,
        //        targetSession,
        //        new PlayerAdd()
        //        {
        //            UserId = userId,
        //            UserName = character.Name,
        //            Island = state?.Island,
        //            Task = state?.Task,
        //            TaskArgument = state?.TaskArgument
        //        }
        //    ));

        //    return true;
        //}

        //public bool Leave(string userId)
        //{
        //    var targetSession = gameData.GetSessionByUserId(userId);
        //    if (targetSession == null) return false;

        //    var character = gameData.GetCharacterByUserId(userId);
        //    if (character == null) return false;

        //    gameData.EnqueueGameEvent(gameData.CreateSessionEvent(
        //        GameEventType.PlayerRemove,
        //        targetSession,
        //        new PlayerId { UserId = userId }
        //    ));

        //    return true;
        //}

        //public bool SetTask(string userId, string task, string taskArgument)
        //{
        //    var targetSession = gameData.GetSessionByUserId(userId);
        //    if (targetSession == null) return false;

        //    var character = gameData.GetCharacterByUserId(userId);
        //    if (character == null) return false;

        //    gameData.EnqueueGameEvent(gameData.CreateSessionEvent(
        //        GameEventType.PlayerTask,
        //        targetSession,
        //        new PlayerTask()
        //        {
        //            UserId = userId,
        //            Task = task,
        //            TaskArgument = taskArgument
        //        }
        //    ));

        //    return true;
        //}

        //public bool JoinRaid(string userId)
        //{
        //    var targetSession = gameData.GetSessionByUserId(userId);
        //    if (targetSession == null) return false;

        //    var character = gameData.GetCharacterByUserId(userId);
        //    if (character == null) return false;

        //    gameData.EnqueueGameEvent(gameData.CreateSessionEvent(
        //        GameEventType.PlayerJoinRaid,
        //        targetSession,
        //        new PlayerId { UserId = userId }
        //    ));

        //    return true;
        //}

        //public bool JoinDungeon(string userId)
        //{
        //    var targetSession = gameData.GetSessionByUserId(userId);
        //    if (targetSession == null) return false;

        //    var character = gameData.GetCharacterByUserId(userId);
        //    if (character == null) return false;

        //    gameData.EnqueueGameEvent(gameData.CreateSessionEvent(
        //        GameEventType.PlayerJoinDungeon,
        //        targetSession,
        //        new PlayerId { UserId = userId }
        //    ));

        //    return true;
        //}

        //public bool JoinArena(string userId)
        //{
        //    var targetSession = gameData.GetSessionByUserId(userId);
        //    if (targetSession == null) return false;

        //    var character = gameData.GetCharacterByUserId(userId);
        //    if (character == null) return false;

        //    gameData.EnqueueGameEvent(gameData.CreateSessionEvent(
        //        GameEventType.PlayerJoinArena,
        //        targetSession,
        //        new PlayerId { UserId = userId }
        //    ));

        //    return true;
        //}

        //public bool DuelAccept(string userId)
        //{
        //    var targetSession = gameData.GetSessionByUserId(userId);
        //    if (targetSession == null) return false;

        //    var character = gameData.GetCharacterByUserId(userId);
        //    if (character == null) return false;

        //    gameData.EnqueueGameEvent(gameData.CreateSessionEvent(
        //        GameEventType.PlayerDuelAccept,
        //        targetSession,
        //        new PlayerId { UserId = userId }
        //    ));

        //    return true;
        //}

        //public bool DuelDecline(string userId)
        //{
        //    var targetSession = gameData.GetSessionByUserId(userId);
        //    if (targetSession == null) return false;

        //    var character = gameData.GetCharacterByUserId(userId);
        //    if (character == null) return false;

        //    gameData.EnqueueGameEvent(gameData.CreateSessionEvent(
        //        GameEventType.PlayerDuelDecline,
        //        targetSession,
        //        new PlayerId { UserId = userId }
        //    ));

        //    return true;
        //}

        //public bool DuelRequest(string userId, string targetUserId)
        //{
        //    var targetSession = gameData.GetSessionByUserId(userId);
        //    if (targetSession == null) return false;

        //    var character = gameData.GetCharacterByUserId(userId);
        //    if (character == null) return false;

        //    gameData.EnqueueGameEvent(gameData.CreateSessionEvent(
        //        GameEventType.PlayerDuelRequest,
        //        targetSession,
        //        new DuelRequest { UserId = userId, TargetUserId = targetUserId }
        //    ));

        //    return true;
        //}

        //public bool Travel(string userId, string island)
        //{
        //    var targetSession = gameData.GetSessionByUserId(userId);
        //    if (targetSession == null) return false;

        //    var character = gameData.GetCharacterByUserId(userId);
        //    if (character == null) return false;

        //    gameData.EnqueueGameEvent(gameData.CreateSessionEvent(
        //        GameEventType.PlayerTravel,
        //        targetSession,
        //        new PlayerTravel { UserId = userId, Island = island }
        //    ));

        //    return true;
        //}
    }
}
