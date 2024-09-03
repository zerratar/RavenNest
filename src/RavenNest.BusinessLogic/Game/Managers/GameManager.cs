using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using RavenNest.Models;
using RavenNest.Sessions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TwitchLib.Api.Helix.Models.Bits;
using static RavenNest.Models.Tv.Episode;

namespace RavenNest.BusinessLogic.Game
{
    public class GameManager
    {
        private readonly ILogger<GameManager> logger;
        private readonly IServerManager serverManager;
        private readonly SessionManager sessionManager;
        private readonly PlayerInventoryProvider inventoryProvider;
        private readonly GameData gameData;

        private readonly Guid expScrollId;
        private readonly Guid dungeonScrollId;
        private readonly Guid raidScrollId;

        public GameManager(
            ILogger<GameManager> logger,
            IServerManager serverManager,
            SessionManager sessionManager,
            PlayerInventoryProvider inventoryProvider,
            GameData gameData)
        {
            this.logger = logger;
            this.serverManager = serverManager;
            this.sessionManager = sessionManager;
            this.inventoryProvider = inventoryProvider;
            this.gameData = gameData;

            var items = gameData.GetKnownItems();

            this.raidScrollId = items.RaidScroll.Id;
            this.dungeonScrollId = items.DungeonScroll.Id;
            this.expScrollId = items.ExpMultiplierScroll.Id;
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

        private static bool IsScrollOfType(ReadOnlyInventoryItem item, ScrollType scrollType)
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

        public bool ClearPlayers(SessionToken session)
        {
            var gameSession = gameData.GetSession(session.SessionId);
            if (gameSession == null)
            {
                return false;
            }

            var characters = gameData.GetCharactersByUserLock(gameSession.UserId);
            foreach (var c in characters)
            {
                if (c.UserIdLock != null)
                    c.PrevUserIdLock = c.UserIdLock;
                c.UserIdLock = null;
            }

            return true;
        }

        public IReadOnlyList<ItemDrop> GetDungeonDropList(int tier)
        {
            /*  DungeonTier
                Common = 0,
                Uncommon = 1,
                Rare = 2,
                Epic = 3,
                Legendary = 4,
                Dynamic = 5
             */

            return gameData.GetItemDrops().Where(x => CanBeDropped(x, tier)).OrderBy(x => System.Random.Shared.Next()).ToList();
        }

        public IReadOnlyList<ItemDrop> GetRaidDropList()
        {
            return gameData.GetItemDrops().Where(x => CanBeDropped(x)).OrderBy(x => System.Random.Shared.Next()).ToList();
        }

        public bool CanBeDropped(ItemDrop itemDrop, int tier = 0)
        {
            if (itemDrop == null) return false;
            if (gameData.GetItem(itemDrop.ItemId) == null)
                return false;

            if (itemDrop.Tier > tier) return false;
            if (itemDrop.DropStartMonth == null || itemDrop.DropStartMonth == 0 || itemDrop.DropDurationMonths == null || itemDrop.DropDurationMonths == 0)
                return true; // no date restriction

            var now = DateTime.UtcNow;
            var startMonth = itemDrop.DropStartMonth.Value;
            var start = new DateTime(now.Year, startMonth, 1);
            var end = start.AddMonths(itemDrop.DropDurationMonths.Value);
            return now >= start && now <= end;
        }

        private ConcurrentDictionary<Guid, DateTime> lastDungeonReward = new ConcurrentDictionary<Guid, DateTime>();
        private ConcurrentDictionary<Guid, DateTime> lastRaidReward = new ConcurrentDictionary<Guid, DateTime>();
        public EventItemReward[] GetDungeonRewardsAsync(SessionToken session, int tier, Guid[] characters)
        {
            try
            {
                if (lastDungeonReward.TryGetValue(session.SessionId, out var dateTime))
                {
                    if (DateTime.UtcNow - dateTime <= TimeSpan.FromSeconds(30))
                    {
                        return new EventItemReward[0];
                    }
                }
                var gameSession = gameData.GetSession(session.SessionId);
                if (gameSession == null) return null;
                var rewards = new List<EventItemReward>();
                var sessionCharacters = gameData.GetActiveSessionCharacters(gameSession);
                var rng = Random.Shared;
                var dropList = GetDungeonDropList(tier);

                var knownItems = gameData.GetKnownItems();

                const double seasonalTokenDropRate = 0.05;

                foreach (var c in characters)
                {
                    var character = sessionCharacters.FirstOrDefault(x => x.Id == c);
                    if (character == null) continue;

                    var value = rng.NextDouble();
                    var dropChance = value >= 0.5 ? 1f : 0.80f;
                    var skills = gameData.GetCharacterSkills(character.SkillsId);

                    var dl = dropList.Where(x => x != null && x.SlayerLevelRequirement <= skills.SlayerLevel).ToList();
                    if (dl.Count == 0) continue;

                    //dropList.OrderByRandomWeighted(x => GetDropRate(x, skills))

                    var tokenDrop = dl.FirstOrDefault(x => x.ItemId == knownItems.HalloweenToken.Id || x.ItemId == knownItems.ChristmasToken.Id);
                    if (tokenDrop != null)
                    {
                        if (rng.NextDouble() <= seasonalTokenDropRate)
                        {
                            var inv = inventoryProvider.Get(character.Id);
                            var stack = inv.AddItem(tokenDrop.ItemId, 1).FirstOrDefault();

                            // log when a seasonal token is dropped so I can keep track on this.

                            logger.LogError("Token Drop from Dungeon from " + (Math.Round(seasonalTokenDropRate * 100)) + "%, Player: " + character.Name);

                            rewards.Add(new EventItemReward
                            {
                                Amount = 1,
                                CharacterId = character.Id,
                                ItemId = tokenDrop.ItemId,
                                InventoryItemId = stack.Id
                            });
                            continue;
                        }
                    }

                    // pick an item at random based on highest drop rate
                    var dropRates = dl.Select((x, index) => new { Name = gameData.GetItem(x.ItemId).Name, DropRate = GetDropRate(x, index, dropList.Count, 0, skills), ItemId = x.ItemId }).ToArray();
                    var item = dropRates.Weighted(x => x.DropRate, rng);

                    if (rng.NextDouble() <= dropChance)
                    {
                        var inv = inventoryProvider.Get(character.Id);
                        var stack = inv.AddItem(item.ItemId, 1).FirstOrDefault();

                        var amount = 1;
                        if (item.Name.Contains("token", StringComparison.OrdinalIgnoreCase))
                        {
                            amount = tier > 0 && rng.NextDouble() >= 0.5 ? 2 : 1;
                            logger.LogError("Token Drop from Dungeon: " + item.Name + ", Player: " + character.Name + ", Amount: " + amount);
                        }

                        rewards.Add(new EventItemReward
                        {
                            Amount = amount,
                            CharacterId = character.Id,
                            ItemId = item.ItemId,
                            InventoryItemId = stack.Id
                        });
                    }
                }

                return rewards.ToArray();
            }
            finally
            {
                lastDungeonReward[session.SessionId] = DateTime.UtcNow;
            }
        }

        public EventItemReward[] GetRaidRewardsAsync(SessionToken session, Guid[] characters)
        {
            try
            {
                if (lastRaidReward.TryGetValue(session.SessionId, out var dateTime))
                {
                    if (DateTime.UtcNow - dateTime <= TimeSpan.FromSeconds(30))
                    {
                        return new EventItemReward[0];
                    }
                }

                var gameSession = gameData.GetSession(session.SessionId);
                if (gameSession == null) return null;
                var rewards = new List<EventItemReward>();
                var sessionCharacters = gameData.GetActiveSessionCharacters(gameSession);
                var rng = Random.Shared;

                var knownItems = gameData.GetKnownItems();

                var dropList = GetRaidDropList();
                var dropChance = 0.5;

                const double seasonalTokenDropRate = 0.025;

                foreach (var c in characters)
                {
                    var character = sessionCharacters.FirstOrDefault(x => x.Id == c);
                    if (character == null) continue;

                    var skills = gameData.GetCharacterSkills(character.SkillsId);

                    var dl = dropList.Where(x => x != null && x.SlayerLevelRequirement <= skills.SlayerLevel).ToList();
                    if (dl.Count == 0) continue;

                    // check if we have a token drop
                    // then flip a coin to see if we should get a token or just select a random item

                    var tokenDrop = dl.FirstOrDefault(x => x.ItemId == knownItems.HalloweenToken.Id || x.ItemId == knownItems.ChristmasToken.Id);
                    if (tokenDrop != null)
                    {
                        if (rng.NextDouble() <= seasonalTokenDropRate)
                        {
                            var inv = inventoryProvider.Get(character.Id);
                            var stack = inv.AddItem(tokenDrop.ItemId, 1).FirstOrDefault();

                            // log when a seasonal token is dropped so I can keep track on this.

                            logger.LogError("Token Drop from Raid from " + (Math.Round(seasonalTokenDropRate * 100)) + "%, Player: " + character.Name);

                            rewards.Add(new EventItemReward
                            {
                                Amount = 1,
                                CharacterId = character.Id,
                                ItemId = tokenDrop.ItemId,
                                InventoryItemId = stack.Id
                            });
                            continue;
                        }
                    }

                    // pick an item at random based on highest drop rate

                    var dropRates = dl.Select((x, index) => new { Name = gameData.GetItem(x.ItemId).Name, DropRate = GetDropRate(x, index, dropList.Count, 0, skills), ItemId = x.ItemId }).ToArray();
                    var item = dropRates.Weighted(x => x.DropRate, rng);

                    var rngVal = rng.NextDouble();
                    if (rngVal <= dropChance)
                    {

                        var inv = inventoryProvider.Get(character.Id);
                        var stack = inv.AddItem(item.ItemId, 1).FirstOrDefault();

                        // log when a seasonal token is dropped so I can keep track on this.

                        if (item.Name.Contains("token", StringComparison.OrdinalIgnoreCase))
                        {
                            logger.LogError("Token Drop from Raid: " + item.Name + ", Player: " + character.Name);
                        }

                        rewards.Add(new EventItemReward
                        {
                            Amount = 1,
                            CharacterId = character.Id,
                            ItemId = item.ItemId,
                            InventoryItemId = stack.Id
                        });
                    }
                }

                return rewards.ToArray();
            }
            finally
            {
                lastRaidReward[session.SessionId] = DateTime.UtcNow;
            }
        }

        private double GetDropRate(ItemDrop drop, int index, int count, int tier, DataModels.Skills skills)
        {
            if (drop.SlayerLevelRequirement > skills.SlayerLevel) return 0;

            var item = gameData.GetItem(drop.ItemId);
            var attackScale = Math.Min(1f, skills.AttackLevel / (float)item.RequiredAttackLevel);
            var defenseScale = Math.Min(1f, skills.DefenseLevel / (float)item.RequiredDefenseLevel);
            var rangedScale = Math.Min(1f, skills.RangedLevel / (float)item.RequiredRangedLevel);
            var magicScale = Math.Min(1f, skills.MagicLevel / (float)item.RequiredMagicLevel);
            var healingScale = Math.Min(1f, Math.Max(magicScale, (skills.HealingLevel / (float)item.RequiredMagicLevel)));

            var levelRequirementFactor = Math.Clamp(attackScale * defenseScale * rangedScale * magicScale * healingScale, 0, 1.25);
            var dropChance = drop.MinDropRate;

            if (drop.DropStartMonth == null)
            {
                dropChance = drop.MaxDropRate * levelRequirementFactor;
            }
            else
            {
                var now = DateTime.UtcNow;
                var start = new DateTime(now.Year, drop.DropStartMonth.Value, 1);
                var end = start.AddMonths(drop.DropDurationMonths.Value);

                dropChance = (now.Date == start || now.Date >= end.AddDays(-1)
                    ? drop.MaxDropRate
                    : GameMath.Lerp(drop.MinDropRate, drop.MaxDropRate, (float)((end - now) / (end - start)))) * levelRequirementFactor;
            }

            var a = Math.Min(1d, (double)index / count);
            var b = Math.Min(1d, skills.SlayerLevel / (double)GameMath.MaxLevel);
            var rate = Math.Max(dropChance * a, dropChance * b);

            // add tier scaling

            return rate;
        }

        #region ingame action methods, commented out

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
        #endregion
    }
}
