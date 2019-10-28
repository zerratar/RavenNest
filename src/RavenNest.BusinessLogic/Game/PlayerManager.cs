using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Net;
using RavenNest.DataModels;
using RavenNest.Models;

using Appearance = RavenNest.DataModels.Appearance;
using Gender = RavenNest.DataModels.Gender;
using InventoryItem = RavenNest.DataModels.InventoryItem;
using Item = RavenNest.DataModels.Item;
using ItemCategory = RavenNest.DataModels.ItemCategory;
using ItemType = RavenNest.DataModels.ItemType;
using Resources = RavenNest.DataModels.Resources;
using Skills = RavenNest.DataModels.Skills;
using Statistics = RavenNest.DataModels.Statistics;

namespace RavenNest.BusinessLogic.Game
{
    public class PlayerManager : IPlayerManager
    {
        private const double PlayerStateUpdateIntervalSeconds = 5.0;
        private const double PlayerCacheDurationSeconds = 30.0;

        private readonly IMemoryCache memoryCache;
        private readonly IGameData gameData;
        private readonly IRavenfallDbContextProvider dbProvider;

        public PlayerManager(IMemoryCache memoryCache, IGameData gameData)
        {
            this.memoryCache = memoryCache;
            this.gameData = gameData;
        }

        public Player CreatePlayerIfNotExistsAsync(string userId, string userName)
        {
            var player = GetGlobalPlayerAsync(userId);
            if (player != null) return player;
            return CreatePlayerAsync(null, userId, userName);
        }

        public Player CreatePlayerAsync(string userId, string userName)
        {
            return CreatePlayerAsync(null, userId, userName);
        }

        public async Task<Player> AddPlayerAsync(SessionToken token, string userId, string userName)
        {
            var session = gameData.GetSession(token.SessionId);
            //var session = await db.GameSession
            //    .Include(x => x.User)
            //    .FirstOrDefaultAsync(
            //    x => x.Id == token.SessionId &&
            //         x.Status == (int)SessionStatus.Active);

            if (session == null || session.Status != (int)SessionStatus.Active)
            {
                return null;
            }

            var user = gameData.GetUser(userId);
            if (user == null)
            {
                return CreatePlayerAsync(session, userId, userName);
            }

            var character = await db.Character
                .Include(x => x.User)
                .Include(x => x.InventoryItem) //.ThenInclude(x => x.Item)
                .Include(x => x.Appearance)
                .Include(x => x.SyntyAppearance)
                .Include(x => x.Resources)
                .Include(x => x.Skills)
                .Include(x => x.Statistics)
                .FirstOrDefaultAsync(x =>
                    x.UserId == user.Id &&
                    ((session.Local && x.Local && x.OriginUserId == session.UserId) ||
                     !session.Local && !x.Local));

            if (character == null)
            {
                return CreatePlayerAsync(session, user);
            }

            if (character.SyntyAppearance == null && character.Appearance != null)
            {
                var syntyApp = GenerateSyntyAppearance(character.Appearance);
                gameData.Add(syntyApp);
                character.SyntyAppearanceId = syntyApp.Id;
                character.SyntyAppearance = syntyApp;
            }

            // check if we need to remove the player from
            // their active session.
            if (character.UserIdLock != null)
            {
                TryRemovePlayerFromPreviousSessionAsync(character, session);
            }

            character.UserIdLock = session.UserId;
            character.LastUsed = DateTime.UtcNow;
            return character.Map(user);
        }

        private void TryRemovePlayerFromPreviousSessionAsync(Character character, GameSession joiningSession)
        {
            var userToRemove = gameData.GetUser(character.UserId);
            if (userToRemove == null)
            {
                return;
            }

            var currentSession = await db.GameSession
                .Include(x => x.GameEvents)
                .Where(x => x.UserId == character.UserIdLock && x.Stopped == null)
                .OrderByDescending(x => x.Started)
                .FirstOrDefaultAsync();

            if (currentSession == null || currentSession.Id == joiningSession.Id || currentSession.UserId == joiningSession.UserId)
            {
                return;
            }

            var revision = currentSession.GameEvents.Count > 0
                ? currentSession.GameEvents.Max(x => x.Revision) + 1 : 1;
            await db.GameEvent.AddAsync(new DataModels.GameEvent()
            {
                Id = Guid.NewGuid(),
                GameSessionId = currentSession.Id,
                GameSession = currentSession,
                Data = JSON.Stringify(new PlayerRemove()
                {
                    Reason =
                        joiningSession != null
                        ? $"{character.Name} joined {joiningSession?.User?.UserName}'s stream"
                        : $"{character.Name} joined another session.",

                    UserId = character.User.UserId
                }),
                Type = (int)GameEventType.PlayerRemove,
                Revision = revision
            });
        }

        public Player GetGlobalPlayerAsync(Guid userId)
        {
            var user = gameData.GetUser(userId);
            if (user == null)
            {
                return null;
            }

            return GetGlobalPlayerAsync(user);
        }

        public Player GetGlobalPlayerAsync(string userId)
        {
            var user = gameData.GetUser(userId);
            if (user == null)
            {
                return null;
            }

            return GetGlobalPlayerAsync(user);
        }

        public async Task<Player> GetPlayerAsync(SessionToken sessionToken)
        {
            using (var db = dbProvider.Get())
            {

                var session = await db.GameSession.FirstOrDefaultAsync(x => x.Id == sessionToken.SessionId);
                if (session == null)
                {
                    return null;
                }

                var user = await db.User.FirstOrDefaultAsync(x => x.Id == session.UserId);
                if (user == null)
                {
                    return null;
                }

                var character = await db.Character
                    .Include(x => x.InventoryItem) //.ThenInclude(x => x.Item)
                    .Include(x => x.State)
                    .Include(x => x.Appearance)
                    .Include(x => x.SyntyAppearance)
                    .Include(x => x.Resources)
                    .Include(x => x.Skills)
                    .Include(x => x.Statistics)
                    .FirstOrDefaultAsync(x =>
                        x.UserId == user.Id &&
                        (session.Local && x.Local && x.OriginUserId == session.UserId ||
                         !session.Local && !x.Local));

                return character.Map(user);
            }
        }

        public async Task<bool> UpdatePlayerStateAsync(
            SessionToken sessionToken,
            CharacterStateUpdate update)
        {
            try
            {
                // add a temporary throttle to avoid spamming server with player state updates.
                // THIS NEEDS TO BE BULKUPDATED!!!!!

                var player = GetCharacterAsync(sessionToken, update.UserId);
                if (player == null)
                {
                    return false;
                }

                if (player.StateId == null)
                {
                    var state = CreateCharacterState(update);
                    gameData.Add(state);
                    player.State = state;
                    player.StateId = state.Id;
                }
                else
                {
                    player.State.DuelOpponent = update.DuelOpponent;
                    player.State.Health = update.Health;
                    player.State.InArena = update.InArena;
                    player.State.InRaid = update.InRaid;
                    player.State.Island = update.Island;
                    player.State.Task = update.Task;
                    player.State.TaskArgument = update.TaskArgument;
                    player.State.X = (decimal)update.Position.X;
                    player.State.Y = (decimal)update.Position.Y;
                    player.State.Z = (decimal)update.Position.Z;
                    gameData.Update(player.State);
                }
                return true;
            }
            catch { return false; }
        }

        public Player GetPlayer(SessionToken sessionToken, string twitchUserId)
        {

            var user = gameData.GetUser(twitchUserId);
            if (user == null)
            {
                return null;
            }

            var session = gameData.GetSession(sessionToken.SessionId);
            if (session == null)
            {
                return null;
            }

            var character = gameData.FindCharacter(x =>
                    x.UserId == user.Id &&
                    (session.Local && x.Local && x.OriginUserId == session.UserId ||
                    !session.Local && !x.Local));

            return character.Map(user);
        }

        public bool UpdateStatistics(SessionToken token, string userId, decimal[] statistics)
        {
            return UpdateStatisticsAsync(token, userId, statistics);
        }

        public bool UpdateResources(
            SessionToken token, string userId, decimal[] resources)
        {
            return UpdateResourcesAsync(token, userId, resources);
        }

        public bool UpdateExperience(
            SessionToken token, string userId, decimal[] experience)
        {
            return UpdateExperienceAsync(token, userId, experience);
        }

        public async Task<bool> UpdateSyntyAppearance(
            SessionToken token, string userId, Models.SyntyAppearance appearance)
        {
            var session = gameData.GetSession(token.SessionId);
            var character = GetCharacterAsync(token, userId);
            if (character == null || character.UserIdLock != session.UserId)
            {
                return false;
            }

            return await UpdateSyntyAppearanceAsync(userId, appearance);
        }


        public async Task<bool[]> UpdateMany(SessionToken token, PlayerState[] states)
        {
            var results = new List<bool>();
            var gameSession = gameData.GetSession(token.SessionId);
            if (gameSession == null)
            {
                return Enumerable.Range(0, states.Length).Select(x => false).ToArray();
            }

            var sessionPlayers = gameData.GetSessionCharacters(gameSession);
            foreach (var state in states)
            {
                var character = gameSession.Local
                    ? sessionPlayers.FirstOrDefault(x => x.User.UserId == state.UserId && x.OriginUserId == gameSession.UserId && x.Local)
                    : sessionPlayers.FirstOrDefault(x => x.User.UserId == state.UserId && !x.Local);

                if (character == null)
                {
                    results.Add(false);
                    continue;
                }

                //if (!await AcquiredUserLockAsync(token, db, character))
                //{
                //    results.Add(false);
                //    continue;
                //}

                try
                {
                    if (state.Experience != null && state.Experience.Length > 0)
                    {
                        UpdateExperienceAsync(token, state.UserId, state.Experience);
                    }

                    if (state.Statistics != null && state.Statistics.Length > 0)
                    {
                        UpdateStatisticsAsync(token, state.UserId, state.Statistics);
                    }

                    if (state.Resources != null && state.Resources.Length > 0)
                    {
                        UpdateResourcesAsync(token, state.UserId, state.Resources);
                    }

                    EquipBestItems(character);

                    character.Revision = character.Revision.GetValueOrDefault() + 1;
                    gameData.Update(character);

                    results.Add(true);
                }
                catch
                {
                    results.Add(false);
                }
            }

            return results.ToArray();
        }

        public AddItemResult AddItem(SessionToken token, string userId, Guid itemId)
        {
            var item = gameData.GetItem(itemId);
            if (item == null) return AddItemResult.Failed;

            var character = GetCharacterAsync(token, userId);
            if (character == null) return AddItemResult.Failed;

            //var characterSession = await GetActiveCharacterSessionAsync(token, userId);
            //if (characterSession == null) return AddItemResult.Failed;
            //var character = characterSession.Character;
            var skills = character.Skills;
            var equippedItems = gameData.FindPlayerItems(character.Id, x => x.Equipped &&
                        x.Item.Type == item.Type &&
                        x.Item.Category == item.Category);

            //var equippedItems = await db.InventoryItem
            //        .Include(x => x.Item)
            //        .Where(x =>
            //            x.CharacterId == character.Id &&
            //            )
            //        .ToListAsync();

            var equipped = false;
            if (equippedItems.Count > 0)
            {
                var bestFirst = equippedItems.OrderByDescending(x => GetItemValue(x.Item));
                var bestEquipped = bestFirst.First();
                if (CanEquipItem(item, skills) && IsItemBetter(item, bestEquipped.Item))
                {
                    equipped = true;
                }

                if (!equipped)
                {
                    bestEquipped.Equipped = true;
                }

                for (var i = 1; i < equippedItems.Count; ++i)
                {
                    equippedItems[i].Equipped = false;
                }

                gameData.UpdateRange(equippedItems);
            }

            var addNew = true;
            if (!equipped)
            {
                var inv = gameData.GetInventoryItem(character.Id, itemId);
                //var inv = await db.InventoryItem
                //    .FirstOrDefaultAsync(x => x.ItemId == itemId && !x.Equipped && x.CharacterId == character.Id);
                if (inv != null)
                {
                    inv.Amount += 1;
                    gameData.Update(inv);
                    addNew = false;
                }
            }

            if (addNew)
            {
                gameData.Add(new InventoryItem
                {
                    Id = Guid.NewGuid(),
                    ItemId = item.Id,
                    Amount = 1,
                    CharacterId = character.Id,
                    Equipped = equipped
                });
            }

            return equipped
                ? AddItemResult.AddedAndEquipped
                : AddItemResult.Added;
        }

        public async Task<bool> GiftItemAsync(
            SessionToken token,
            string gifterUserId,
            string receiverUserId,
            Guid itemId)
        {
            return false;
        }

        public async Task<bool> GiftResourcesAsync(
            SessionToken token,
            string giftUserId,
            string receiverUserId,
            string resource,
            long amount)
        {
            return false;
        }

        public bool EquipItemAsync(SessionToken token, string userId, Guid itemId)
        {
            var character = GetCharacterAsync(token, userId);
            if (character == null) return false;

            var item = gameData.GetItem(itemId);
            if (item == null) return false;

            var invItem = gameData.GetInventoryItem(character.Id, itemId);
            //var invItem = await db.InventoryItem
            //    .Include(x => x.Item)
            //    .FirstOrDefaultAsync(x =>
            //        x.CharacterId == character.Id &&
            //        x.ItemId == itemId &&
            //        !x.Equipped);

            var skills = character.Skills;
            if (invItem == null || !CanEquipItem(invItem.Item, skills))
                return false;

            if (invItem.Amount > 1)
            {
                invItem.Amount = invItem.Amount - 1;
                gameData.Update(invItem);

                invItem = new InventoryItem
                {
                    Id = Guid.NewGuid(),
                    CharacterId = character.Id,
                    Character = character,
                    Amount = 1,
                    Equipped = true,
                    ItemId = invItem.ItemId,
                    Item = item
                };
                gameData.Add(invItem);
            }
            else
            {
                invItem.Equipped = true;
                gameData.Update(invItem);
            }
            InventoryItem invItemEq = gameData.FindPlayerItem(character.Id,
                x => x.Item.Type == item.Type &&
                x.Item.Category == item.Category && x.Equipped);

            //var invItemEq = await db.InventoryItem
            //    .Include(x => x.Item)
            //    .FirstOrDefaultAsync(x =>
            //        x.CharacterId == character.Id &&
            //        x.Item.Type == item.Type &&
            //        x.Item.Category == item.Category && x.Equipped);

            if (invItemEq != null)
            {
                var stack = gameData.GetInventoryItem(character.Id, invItemEq.ItemId);
                //var stack = await db.InventoryItem
                //.Include(x => x.Item)
                //.FirstOrDefaultAsync(x =>
                //    x.CharacterId == character.Id &&
                //    x.ItemId == invItemEq.ItemId &&
                //    !x.Equipped);

                if (stack != null)
                {
                    ++stack.Amount;
                    gameData.Remove(invItemEq);
                    gameData.Update(stack);
                }
                else
                {
                    invItemEq.Equipped = false;
                    gameData.Update(invItemEq);
                }
            }

            return true;
        }

        public bool UnEquipItemAsync(SessionToken token, string userId, Guid itemId)
        {
            var character = GetCharacterAsync(token, userId);
            if (character == null) return false;

            var invItem = gameData.GetEquippedItem(character.Id, itemId);
            //var invItem = await db.InventoryItem
            //    .Include(x => x.Item)
            //    .FirstOrDefaultAsync(x =>
            //        x.CharacterId == character.Id &&
            //        x.ItemId == itemId &&
            //        x.Equipped);

            if (invItem == null) return false;
            var stack = gameData.GetInventoryItem(character.Id, itemId);
            //var stack = await db.InventoryItem
            //    .Include(x => x.Item)
            //    .FirstOrDefaultAsync(x =>
            //        x.CharacterId == character.Id &&
            //        x.ItemId == itemId &&
            //        !x.Equipped);

            if (stack != null)
            {
                ++stack.Amount;
                gameData.Remove(invItem);
                gameData.Update(stack);
            }
            else
            {
                invItem.Equipped = false;
                gameData.Update(invItem);
            }
            return true;
        }

        public ItemCollection GetEquippedItemsAsync(SessionToken token, string userId)
        {
            var itemCollection = new ItemCollection();
            var character = GetCharacterAsync(token, userId);
            if (character == null) return itemCollection;
            /*
                .Include(x => x.Item)
                .Where(x => x.CharacterId == character.Id && x.Equipped)            
             */

            var items = gameData.GetEquippedItems(character.Id);
            foreach (var inv in items)
            {
                itemCollection.Add(ModelMapper.Map(inv.Item));
            }

            return itemCollection;
        }

        public ItemCollection GetAllItemsAsync(SessionToken token, string userId)
        {
            var itemCollection = new ItemCollection();
            var character = GetCharacterAsync(token, userId);
            if (character == null) return itemCollection;

            var items = gameData.GetAllPlayerItems(character.Id);
            foreach (var inv in items)
            {
                itemCollection.Add(ModelMapper.Map(inv.Item));
            }

            return null;
        }

        public IReadOnlyList<Player> GetPlayersAsync()
        {
            var characters = gameData.GetCharacters(x => !x.Local);
            return characters.Select(x => x.Map(x.User)).ToList();
        }

        private bool UpdateStatisticsAsync(SessionToken token, string userId, decimal[] statistics)
        {
            var character = GetCharacterAsync(token, userId);
            if (character == null) return false;
            if (!AcquiredUserLockAsync(token, character)) return false;

            var index = 0;

            character.Statistics.RaidsWon += (int)statistics[index++];
            character.Statistics.RaidsLost += (int)statistics[index++];
            character.Statistics.RaidsJoined += (int)statistics[index++];

            character.Statistics.DuelsWon += (int)statistics[index++];
            character.Statistics.DuelsLost += (int)statistics[index++];

            character.Statistics.PlayersKilled += (int)statistics[index++];
            character.Statistics.EnemiesKilled += (int)statistics[index++];

            character.Statistics.ArenaFightsJoined += (int)statistics[index++];
            character.Statistics.ArenaFightsWon += (int)statistics[index++];

            character.Statistics.TotalDamageDone += (long)statistics[index++];
            character.Statistics.TotalDamageTaken += (long)statistics[index++];
            character.Statistics.DeathCount += (int)statistics[index++];

            character.Statistics.TotalWoodCollected += statistics[index++];
            character.Statistics.TotalOreCollected += statistics[index++];
            character.Statistics.TotalFishCollected += statistics[index++];
            character.Statistics.TotalWheatCollected += statistics[index++];

            character.Statistics.CraftedWeapons += (int)statistics[index++];
            character.Statistics.CraftedArmors += (int)statistics[index++];
            character.Statistics.CraftedPotions += (int)statistics[index++];
            character.Statistics.CraftedRings += (int)statistics[index++];
            character.Statistics.CraftedAmulets += (int)statistics[index++];

            character.Statistics.CookedFood += (int)statistics[index++];

            character.Statistics.ConsumedPotions += (int)statistics[index++];
            character.Statistics.ConsumedFood += (int)statistics[index++];

            character.Statistics.TotalTreesCutDown += (long)statistics[index];

            gameData.Update(character.Appearance);
            return false;
        }

        public async Task<bool> UpdateSyntyAppearanceAsync(string userId, Models.SyntyAppearance appearance)
        {
            try
            {
                using (var db = dbProvider.Get())
                {
                    var user = await db.User.FirstOrDefaultAsync(x => x.UserId == userId);
                    if (user == null) return false;

                    var character = await db.Character
                        .Include(x => x.SyntyAppearance)
                        .FirstOrDefaultAsync(x => !x.Local && x.UserId == user.Id);

                    if (character == null) return false;

                    UpdateCharacterAppearance(appearance, character);

                    var sessionOwnerUserId = character.UserIdLock;
                    var gameSession = await db.GameSession
                        .Where(x => x.UserId == sessionOwnerUserId && x.Status == (int)SessionStatus.Active)
                        .OrderByDescending(x => x.Started)
                        .FirstOrDefaultAsync();

                    if (gameSession != null)
                    {
                        var lastEvent = await db.GameEvent
                            .Where(x => x.GameSessionId == gameSession.Id)
                            .OrderByDescending(x => x.Revision)
                            .FirstOrDefaultAsync();

                        var gameEvent = new DataModels.GameEvent
                        {
                            Id = Guid.NewGuid(),
                            GameSessionId = gameSession.Id,
                            Revision = (lastEvent?.Revision).GetValueOrDefault() + 1,
                            Type = (int)GameEventType.PlayerAppearance,
                            Data = JSON.Stringify(new SyntyAppearanceUpdate
                            {
                                UserId = userId,
                                Value = appearance
                            })
                        };

                        await db.GameEvent.AddAsync(gameEvent);
                    }

                    await db.SaveChangesAsync();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public int[] ToAppearanceData(Appearance appearance)
        {
            return new int[]
            {
                (int)appearance.Gender,
                appearance.Gender == Gender.Female ? appearance.FemaleHairModelNumber : appearance.MaleHairModelNumber,
                (int)appearance.HairColor,
                appearance.EyesModelNumber,
                (int)appearance.SkinColor,
                appearance.BeardModelNumber,
                (int)appearance.BeardColor,
                appearance.BrowsModelNumber,
                (int)appearance.BrowColor,
                appearance.MouthModelNumber
            };
        }

        private Player GetGlobalPlayerAsync(User user)
        {
            var character = gameData.FindCharacter(x => x.UserId == user.Id && !x.Local);
            return character.Map(user);
        }

        private void UpdateCharacterAppearance(
            Models.SyntyAppearance appearance, Character character)
        {
            DataMapper.RefMap(appearance, character.SyntyAppearance, nameof(appearance.Id));
            gameData.Update(character.SyntyAppearance);
        }

        private bool UpdateExperienceAsync(
            SessionToken token,
            string userId,
            decimal[] experience)
        {
            try
            {
                var character = GetCharacterAsync(token, userId);
                if (character == null) return false;
                if (!AcquiredUserLockAsync(token, character)) return false;

                var skillIndex = 0;
                var attackExperience = Math.Max(character.Skills.Attack, experience[skillIndex++]);
                var defenseExperience = Math.Max(character.Skills.Defense, experience[skillIndex++]);

                character.Skills.Attack = attackExperience;
                character.Skills.Defense = defenseExperience;
                character.Skills.Strength = Math.Max(character.Skills.Strength, experience[skillIndex++]);
                character.Skills.Health = Math.Max(character.Skills.Health, experience[skillIndex++]);
                character.Skills.Woodcutting = Math.Max(character.Skills.Woodcutting, experience[skillIndex++]);
                character.Skills.Fishing = Math.Max(character.Skills.Fishing, experience[skillIndex++]);
                character.Skills.Mining = Math.Max(character.Skills.Mining, experience[skillIndex++]);
                character.Skills.Crafting = Math.Max(character.Skills.Crafting, experience[skillIndex++]);
                character.Skills.Cooking = Math.Max(character.Skills.Cooking, experience[skillIndex++]);
                character.Skills.Farming = Math.Max(character.Skills.Farming, experience[skillIndex++]);

                if (experience.Length > 10) character.Skills.Slayer = Math.Max(character.Skills.Slayer, experience[skillIndex++]);
                if (experience.Length > 11) character.Skills.Magic = Math.Max(character.Skills.Magic, experience[skillIndex++]);
                if (experience.Length > 12) character.Skills.Ranged = Math.Max(character.Skills.Ranged, experience[skillIndex++]);
                if (experience.Length > 13) character.Skills.Sailing = Math.Max(character.Skills.Sailing, experience[skillIndex++]);

                gameData.Update(character.Skills);
                return true;
            }
            catch
            {
                return false;
            }
        }
        public void EquipBestItems(Character character)
        {
            var weapons = character.InventoryItem
                .Where(x => x.Item.Category == (int)ItemCategory.Weapon)
                .OrderByDescending(x => GetItemValue(x.Item))
                .ToList();

            if (weapons.Count > 0)
            {
                var weaponToEquip = weapons.FirstOrDefault(x => CanEquipItem(x.Item, character.Skills));
                if (weaponToEquip != null)
                {
                    weaponToEquip.Equipped = true;
                }
                foreach (var weapon in weapons.Where(x => x != weaponToEquip))
                {
                    weapon.Equipped = false;
                }

                gameData.UpdateRange(weapons);
            }

            foreach (var itemGroup in character.InventoryItem
                .Where(x => x.Item.Category != (int)ItemCategory.Weapon)
                .GroupBy(x => x.Item.Type))
            {
                var itemToEquip = itemGroup
                    .OrderByDescending(x => GetItemValue(x.Item))
                    .FirstOrDefault(x => CanEquipItem(x.Item, character.Skills));

                // ensure we don't change equipped pet
                if (itemGroup.Key == (int)ItemType.Pet)
                {
                    var alreadyEquipped = itemGroup.FirstOrDefault(x => x.Equipped);
                    if (alreadyEquipped != null)
                    {
                        itemToEquip = alreadyEquipped;
                    }
                }

                if (itemToEquip != null)
                {
                    itemToEquip.Equipped = true;

                    if (itemToEquip.Amount > 1)
                    {
                        var diff = itemToEquip.Amount - 1;
                        var inventoryItem = character.InventoryItem
                            .FirstOrDefault(x => x.ItemId == itemToEquip.ItemId && !x.Equipped);

                        if (inventoryItem == null)
                        {
                            gameData.Add(new InventoryItem
                            {
                                Id = Guid.NewGuid(),
                                ItemId = itemToEquip.ItemId,
                                CharacterId = itemToEquip.CharacterId,
                                Equipped = false,
                                Amount = diff
                            });
                        }
                        else
                        {
                            inventoryItem.Amount += diff;
                            gameData.Update(inventoryItem);
                        }

                        itemToEquip.Amount = 1;
                    }

                    gameData.Update(itemToEquip);
                }

                foreach (var item in itemGroup.Where(x => x != itemToEquip))
                {
                    item.Equipped = false;
                    gameData.Update(item);
                }

                if (itemToEquip != null)
                {
                    gameData.Update(itemToEquip);
                }
            }
        }

        private bool UpdateResourcesAsync(
            SessionToken token,
            string userId,
            decimal[] resources)
        {
            try
            {
                var index = 0;

                var character = GetCharacterAsync(token, userId);
                if (character == null) return false;
                if (!AcquiredUserLockAsync(token, character)) return false;

                // UpdateDeltaClamped(resource, 0, resourceAmount)
                character.Resources.Wood += resources[index++];
                character.Resources.Fish += resources[index++];
                character.Resources.Ore += resources[index++];
                character.Resources.Wheat += resources[index++];
                character.Resources.Coins += resources[index++];
                character.Resources.Magic += resources[index++];
                character.Resources.Arrows += resources[index];

                gameData.Update(character.Resources);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private Player CreatePlayerAsync(
            GameSession session, string userId, string userName)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                UserName = userName,
                Created = DateTime.UtcNow
            };

            gameData.Add(user);
            return CreatePlayerAsync(session, user);
        }

        private Player CreatePlayerAsync(GameSession session, User user)
        {
            var character = new Character
            {
                Id = Guid.NewGuid(),
                Name = user.UserName,
                UserId = user.Id,
                OriginUserId = session?.UserId ?? Guid.Empty,
                Local = session?.Local ?? false,
                Created = DateTime.UtcNow
            };

            var appearance = GenerateRandomAppearance();
            var syntyAppearance = GenerateRandomSyntyAppearance();

            var skills = GenerateSkills();
            var resources = GenerateResources();
            var statistics = GenerateStatistics();

            gameData.Add(syntyAppearance);
            gameData.Add(statistics);
            gameData.Add(skills);
            gameData.Add(appearance);
            gameData.Add(resources);

            character.SyntyAppearanceId = syntyAppearance.Id;
            character.SyntyAppearance = syntyAppearance;
            character.Resources = resources;
            character.ResourcesId = resources.Id;
            character.Appearance = appearance;
            character.AppearanceId = appearance.Id;
            character.Statistics = statistics;
            character.StatisticsId = statistics.Id;
            character.Skills = skills;
            character.SkillsId = skills.Id;
            character.UserIdLock = session?.UserId;
            character.LastUsed = DateTime.UtcNow;
            gameData.Add(character);
            return character.Map(user);
        }

        private Statistics GenerateStatistics()
        {
            return new Statistics
            {
                Id = Guid.NewGuid()
            };
        }

        private Resources GenerateResources()
        {
            return new Resources
            {
                Id = Guid.NewGuid()
            };
        }

        private Skills GenerateSkills()
        {
            return new Skills
            {
                Id = Guid.NewGuid(),
                Health = 1154
            };
        }

        private Character GetCharacterAsync(SessionToken token, string userId)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null) return null;

            var user = gameData.GetUser(userId);
            if (user == null) return null;

            return gameData.FindCharacter(x => x.UserId == user.Id &&
                    (session.Local && x.Local && x.OriginUserId == session.UserId ||
                     !session.Local && !x.Local));
        }

        private DataModels.CharacterState CreateCharacterState(CharacterStateUpdate update)
        {
            var state = new DataModels.CharacterState();
            state.Id = Guid.NewGuid();
            state.DuelOpponent = update.DuelOpponent;
            state.Health = update.Health;
            state.InArena = update.InArena;
            state.InRaid = update.InRaid;
            state.Island = update.Island;
            state.Task = update.Task;
            state.TaskArgument = update.TaskArgument;
            state.X = (decimal)update.Position.X;
            state.Y = (decimal)update.Position.Y;
            state.Z = (decimal)update.Position.Z;
            return state;
        }

        private static DataModels.SyntyAppearance GenerateRandomSyntyAppearance()
        {
            var gender = Utility.Random<Gender>();
            return new DataModels.SyntyAppearance
            {
                Id = Guid.NewGuid(),
                Gender = gender,
                SkinColor = GetHexColor(Utility.Random<DataModels.SkinColor>()),
                HairColor = GetHexColor(Utility.Random<DataModels.HairColor>()),
                BeardColor = GetHexColor(Utility.Random<DataModels.HairColor>()),
                EyeColor = "#000000",
                Eyebrows = Utility.Random(0, gender == Gender.Male ? 10 : 7),
                Hair = Utility.Random(0, 38),
                FacialHair = gender == Gender.Male ? Utility.Random(0, 18) : -1,
                Head = Utility.Random(0, 23),
                HelmetVisible = true
            };
        }

        private static DataModels.SyntyAppearance GenerateSyntyAppearance(Appearance appearance)
        {
            return new DataModels.SyntyAppearance
            {
                Id = Guid.NewGuid(),
                Gender = appearance.Gender,
                SkinColor = GetHexColor(appearance.SkinColor),
                HairColor = GetHexColor(appearance.HairColor),
                BeardColor = GetHexColor(appearance.BeardColor),
                Hair = 0,
                FacialHair = 0,
                Head = 0,
                Eyebrows = 0,
                EyeColor = "#000000",
                HelmetVisible = appearance.HelmetVisible
            };
        }

        private static string GetHexColor(DataModels.HairColor color)
        {
            switch (color)
            {
                case DataModels.HairColor.Blonde:
                    return "#A8912A";
                case DataModels.HairColor.Blue:
                    return "#0D9BB9";
                case DataModels.HairColor.Brown:
                    return "#3C2823";
                case DataModels.HairColor.Grey:
                    return "#595959";
                case DataModels.HairColor.Pink:
                    return "#DF62C7";
                case DataModels.HairColor.Red:
                    return "#C52A4A";
                default:
                    return "#000000";
            }
        }

        private static string GetHexColor(DataModels.SkinColor color)
        {
            switch (color)
            {
                case DataModels.SkinColor.Light:
                    return "#d6b8ae";
                case DataModels.SkinColor.Medium:
                    return "#faa276";
                default:
                    return "#40251e";
            }
        }

        private static Appearance GenerateRandomAppearance()
        {
            return new Appearance
            {
                Id = Guid.NewGuid(),
                Gender = Utility.Random<DataModels.Gender>(),
                SkinColor = Utility.Random<DataModels.SkinColor>(),
                HairColor = Utility.Random<DataModels.HairColor>(),
                BrowColor = Utility.Random<DataModels.HairColor>(),
                BeardColor = Utility.Random<DataModels.HairColor>(),
                EyeColor = Utility.Random<DataModels.EyeColor>(),
                CostumeColor = Utility.Random<DataModels.CostumeColor>(),
                BaseModelNumber = Utility.Random(1, 20),
                TorsoModelNumber = Utility.Random(1, 7),
                BottomModelNumber = Utility.Random(1, 7),
                FeetModelNumber = 1,
                HandModelNumber = 1,
                BeltModelNumber = Utility.Random(0, 10),
                EyesModelNumber = Utility.Random(1, 7),
                BrowsModelNumber = Utility.Random(1, 15),
                MouthModelNumber = Utility.Random(1, 10),
                MaleHairModelNumber = Utility.Random(0, 10),
                FemaleHairModelNumber = Utility.Random(0, 20),
                BeardModelNumber = Utility.Random(0, 10)
            };
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AcquiredUserLock(GameSession session, Character character)
        {
            return character.UserIdLock == session.UserId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AcquiredUserLockAsync(SessionToken token, Character character)
        {
            var session = gameData.GetSession(token.SessionId);
            return character.UserIdLock == session.UserId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CanEquipItem(Item item, Skills skills)
        {
            return item.RequiredDefenseLevel <= GameMath.ExperienceToLevel(skills.Defense) &&
                   item.RequiredAttackLevel <= GameMath.ExperienceToLevel(skills.Attack);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsItemBetter(Item itemA, Item itemB)
        {
            var valueA = GetItemValue(itemA);
            var valueB = GetItemValue(itemB);
            return valueA > valueB;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetItemValue(Item itemA)
        {
            return itemA.Level + itemA.WeaponAim + itemA.WeaponPower + itemA.ArmorPower;
        }
    }
}
