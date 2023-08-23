using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using RavenNest.Models;
using RavenNest.Sessions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TwitchLib.Api.Helix.Models.Bits;

namespace RavenNest.BusinessLogic.Game
{
    public class GameManager
    {
        private readonly IServerManager serverManager;
        private readonly SessionManager sessionManager;
        private readonly PlayerInventoryProvider inventoryProvider;
        private readonly GameData gameData;

        private readonly Guid expScrollId;
        private readonly Guid dungeonScrollId;
        private readonly Guid raidScrollId;

        public GameManager(
            IServerManager serverManager,
            SessionManager sessionManager,
            PlayerInventoryProvider inventoryProvider,
            GameData gameData)
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

            return gameData.GetItemDrops().Where(x => CanBeDropped(x, tier)).ToList();
        }

        public IReadOnlyList<ItemDrop> GetRaidDropList()
        {
            return gameData.GetItemDrops().Where(x => CanBeDropped(x)).ToList();
        }

        public bool CanBeDropped(ItemDrop itemDrop, int tier = 0)
        {
            if (itemDrop.Tier != tier) return false;
            if (itemDrop.DropStartMonth == null || itemDrop.DropStartMonth == 0 || itemDrop.DropDurationMonths == null || itemDrop.DropDurationMonths == 0)
                return true; // no date restriction

            var now = DateTime.UtcNow;
            var startMonth = itemDrop.DropStartMonth.Value;
            var start = new DateTime(now.Year, startMonth, 1);
            var end = start.AddDays(itemDrop.DropDurationMonths.Value);
            return now >= start && now <= end;
        }


        public EventItemReward[] GetDungeonRewardsAsync(SessionToken session, int tier, Guid[] characters)
        {
            var gameSession = gameData.GetSession(session.SessionId);
            if (gameSession == null) return null;
            var rewards = new List<EventItemReward>();
            var sessionCharacters = gameData.GetActiveSessionCharacters(gameSession);
            var rng = Random.Shared;
            var dropList = GetDungeonDropList(tier);

            foreach (var c in characters)
            {
                var character = sessionCharacters.FirstOrDefault(x => x.Id == c);
                if (character == null) continue;

                var value = rng.NextDouble();
                var dropChance = value >= 0.75 ? 1f : 0.75f;
                var skills = gameData.GetCharacterSkills(character.SkillsId);

                var dl = dropList.Where(x => x.SlayerLevelRequirement <= skills.SlayerLevel).ToList();
                if (dl.Count == 0) continue;

                //dropList.OrderByRandomWeighted(x => GetDropRate(x, skills))

                // pick an item at random based on highest drop rate
                var item = dl.Weighted((x, index) => GetDropRate(x, index, dropList.Count, tier, skills));

                if (rng.NextDouble() <= dropChance)
                {
                    rewards.Add(new EventItemReward
                    {
                        Amount = 1,
                        CharacterId = character.Id,
                        ItemId = item.ItemId
                    });
                }
            }

            return rewards.ToArray();
        }

        public EventItemReward[] GetRaidRewardsAsync(SessionToken session, Guid[] characters)
        {
            var gameSession = gameData.GetSession(session.SessionId);
            if (gameSession == null) return null;
            var rewards = new List<EventItemReward>();
            var sessionCharacters = gameData.GetActiveSessionCharacters(gameSession);
            var rng = Random.Shared;

            var dropList = GetRaidDropList();
            var dropChance = 0.25;
            foreach (var c in characters)
            {
                var character = sessionCharacters.FirstOrDefault(x => x.Id == c);
                if (character == null) continue;

                var skills = gameData.GetCharacterSkills(character.SkillsId);

                var dl = dropList.Where(x => x.SlayerLevelRequirement <= skills.SlayerLevel).ToList();
                if (dl.Count == 0) continue;
                // pick an item at random based on highest drop rate
                var item = dl.Weighted((x, index) => GetDropRate(x, index, dropList.Count, 0, skills));

                if (rng.NextDouble() <= dropChance)
                {
                    rewards.Add(new EventItemReward
                    {
                        Amount = 1,
                        CharacterId = character.Id,
                        ItemId = item.ItemId
                    });
                }
            }

            return rewards.ToArray();
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

    public class HeroicTierDropList
    {
        public readonly static List<ItemDrop> itemDrops = new List<ItemDrop>
        {
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("17c3f9b1-57d6-4219-bbc7-9e929757babf"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("d3966d4a-ef1b-4fcc-8695-aa2a823e8b7b"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("67dbf467-07df-4731-9694-1389d07e886f"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("061baa06-5b73-4bbb-a9e1-aea4907cd309"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("c95ac1d6-108e-4b2f-9db2-2ef00c092bfe"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("da0179be-2ef0-412d-8e18-d0ee5a9510c7"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("f6ff9315-c473-4365-a19f-4df697049475"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("0c499637-7316-4c93-a847-216d6750bf34"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("365054c5-cef7-4ac7-a0bf-9711a8610709"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("a8fe9b55-d2be-4219-b9b6-aaaa5150681c"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("99454c89-d3fd-4e33-95eb-08ede2f532d7"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("9a12d35a-0743-47a4-a6f7-10f9a84492ea"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("c512f4e9-d14f-4b50-9205-732c25bb2ee1"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("443ee95a-9c28-49c8-af5b-255c19d656ac"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("94f5568c-bc04-400f-9b56-9bf0a007c66f"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("328b248c-35ac-409e-83e4-ab801a3b9cb1"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("3d641d63-dadf-40dc-ad31-3e741a705532"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("ed01a446-1f9d-4eca-981e-cfd6e7415a4e"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("31dd76be-0fb1-4891-8cf7-045e86c60fd3"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("c3fae08a-a72a-4d86-b213-bb3ca4786f8c"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("21a9e2c3-49e4-4c07-aa38-6def771f51cc"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("311a24ae-3b8d-497e-9823-da3eba2359fb"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("49d53a1e-55f7-4537-9a5b-0560b1c0f465"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("3f53fecf-b913-4dda-8c63-08b724d4914c"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("6b9cc4d1-0e9c-4e90-b474-abac5548e5fd"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("aeaf6b0f-6ebf-4728-8f0e-900f9fb81d6e"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("e317461e-d8e7-495c-a1b6-df0a967ddb71"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("c9f2ee83-9a60-428c-bf3b-997e916965cb"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("44297b32-ac17-4912-9bb2-dc5fa3a3f84e"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("c8ce4210-4980-432c-82eb-9f959386fc31"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("58660f5a-e307-49ff-9bd7-7f3e00c9d9e6"), MinDropRate = 0.01, MaxDropRate = 0.01, Tier = 4 },
        };
    }

    public class NormalTierDropList
    {
        public readonly static List<ItemDrop> itemDrops = new List<ItemDrop>
        {
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("e7aded94-3a28-4bd5-ae37-5b5e63884b53"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("c4479a57-7603-4a7d-bd45-649bc2332509"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("1a2fb90a-2a53-42ed-bf81-92b1cb4fc219"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("27aed634-092c-4e66-a469-953d15b3457e"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("8ca53da1-33e3-4d4f-80f0-38c3bf155b63"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("e147e236-3417-4e28-a639-995b1f45bebc"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("83e9370d-5436-4c44-aa85-7aab4f12912f"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("f9b7e6a3-4e4a-4e4a-b79d-42a3cf2a16c8"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("17c3f9b1-57d6-4219-bbc7-9e929757babf"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("d0b7149e-5362-49f1-b709-190618695e46"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("c3108188-330c-407b-a1dd-3ac628124d74"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("5bb9cf64-81a0-4c10-ab6d-5d4e76eb1de2"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("0dc620c2-b726-4928-9f1c-fcf61aaa2542"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("d3966d4a-ef1b-4fcc-8695-aa2a823e8b7b"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("8975dc29-f9b4-4610-83c0-f00dd1a98c34"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("67dbf467-07df-4731-9694-1389d07e886f"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("061baa06-5b73-4bbb-a9e1-aea4907cd309"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("c95ac1d6-108e-4b2f-9db2-2ef00c092bfe"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("da0179be-2ef0-412d-8e18-d0ee5a9510c7"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("f6ff9315-c473-4365-a19f-4df697049475"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("0c499637-7316-4c93-a847-216d6750bf34"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("365054c5-cef7-4ac7-a0bf-9711a8610709"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("a8fe9b55-d2be-4219-b9b6-aaaa5150681c"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("99454c89-d3fd-4e33-95eb-08ede2f532d7"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("9a12d35a-0743-47a4-a6f7-10f9a84492ea"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("c512f4e9-d14f-4b50-9205-732c25bb2ee1"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("443ee95a-9c28-49c8-af5b-255c19d656ac"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("94f5568c-bc04-400f-9b56-9bf0a007c66f"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("328b248c-35ac-409e-83e4-ab801a3b9cb1"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("3d641d63-dadf-40dc-ad31-3e741a705532"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("ed01a446-1f9d-4eca-981e-cfd6e7415a4e"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("3ab15974-93dd-4864-9e88-94795c7740c9"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("e32a6f17-653c-4af3-a3a1-d0c6674fe4d5"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("3336dee3-222f-4ae5-951f-573b2cacabb6"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("1df7d697-9abd-433d-9db0-786456f9c40c"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("f531d897-5bc3-4ee9-ba47-0160e653a295"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("28653827-3edd-498a-8bb6-1d02583015c2"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("31dd76be-0fb1-4891-8cf7-045e86c60fd3"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("736e2478-bbee-4d58-b60f-904cb57c6067"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("533cf2e8-2815-4601-9f47-f22d6d572366"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("b812a722-1817-48c8-a290-16dc92f14d64"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("aefd1abd-6843-42ec-93e1-0b718be068ce"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("61978101-3dc9-4a4f-a9e8-42465ff2be47"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("9b20661a-e0dc-4a70-868b-5d6f34492c34"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("00aefbe9-9f2d-42c0-9a7c-ca76d55e9cc2"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("fc61bf6c-7b5e-40b4-a06a-693ef9504bc9"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("09c60c0c-94fa-4efe-953e-d98ddae79f11"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("c3fae08a-a72a-4d86-b213-bb3ca4786f8c"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("21a9e2c3-49e4-4c07-aa38-6def771f51cc"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("311a24ae-3b8d-497e-9823-da3eba2359fb"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("49d53a1e-55f7-4537-9a5b-0560b1c0f465"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("3f53fecf-b913-4dda-8c63-08b724d4914c"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("6b9cc4d1-0e9c-4e90-b474-abac5548e5fd"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("311234f8-b836-4d59-8d8a-48696e83b6a8"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("aeaf6b0f-6ebf-4728-8f0e-900f9fb81d6e"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("e317461e-d8e7-495c-a1b6-df0a967ddb71"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("c9f2ee83-9a60-428c-bf3b-997e916965cb"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("ea885b65-3ce2-4adb-a4c6-59135113edfc"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("5cdc8ce0-d1ef-4e20-9bec-51aa90ce51bd"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("44297b32-ac17-4912-9bb2-dc5fa3a3f84e"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("c8ce4210-4980-432c-82eb-9f959386fc31"), MinDropRate = 0.01, MaxDropRate = 0.01 },
             new ItemDrop { Id = Guid.NewGuid(), ItemId = new Guid("073d078b-13e2-4b9a-8fb4-6401971614e4"), MinDropRate = 0.01, MaxDropRate = 0.01 },
        };
    }
}
