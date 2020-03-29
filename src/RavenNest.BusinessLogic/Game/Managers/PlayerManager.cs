﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
using Resources = RavenNest.DataModels.Resources;
using Skills = RavenNest.DataModels.Skills;
using Statistics = RavenNest.DataModels.Statistics;

namespace RavenNest.BusinessLogic.Game
{
    public class PlayerManager : IPlayerManager
    {
        private readonly ILogger logger;
        private readonly IGameData gameData;
        private readonly IIntegrityChecker integrityChecker;


        public PlayerManager(
            ILogger logger,
            IGameData gameData,
            IIntegrityChecker integrityChecker)
        {
            this.logger = logger;
            this.gameData = gameData;
            this.integrityChecker = integrityChecker;
        }

        public Player CreatePlayerIfNotExists(string userId, string userName)
        {
            var player = GetPlayer(userId);
            if (player != null) return player;
            return CreatePlayer(null, userId, userName);
        }

        public Player CreatePlayer(string userId, string userName)
        {
            return CreatePlayer(null, userId, userName);
        }

        public Player AddPlayer(SessionToken token, string userId, string userName)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null || session.Status != (int)SessionStatus.Active)
            {
                return null;
            }

            var user = gameData.GetUser(userId);
            if (user == null)
            {
                return CreatePlayer(session, userId, userName);
            }

            var character = gameData.GetCharacterByUserId(user.Id);
            if (character == null)
            {
                return CreatePlayer(session, user);
            }

            // check if we need to remove the player from
            // their active session.
            if (character.UserIdLock != null)
            {
                TryRemovePlayerFromPreviousSession(character, session);
            }

            character.UserIdLock = session.UserId;
            character.LastUsed = DateTime.UtcNow;
            return character.Map(gameData, user);
        }

        private void TryRemovePlayerFromPreviousSession(Character character, DataModels.GameSession joiningSession)
        {
            var userToRemove = gameData.GetUser(character.UserId);
            if (userToRemove == null)
            {
                return;
            }

            var currentSession = gameData.GetUserSession(character.UserIdLock.GetValueOrDefault());
            if (currentSession == null || currentSession.Id == joiningSession.Id || currentSession.UserId == joiningSession.UserId)
            {
                return;
            }

            var targetSessionUser = gameData.GetUser(joiningSession.UserId);
            var characterUser = gameData.GetUser(character.UserId);
            var gameEvent = gameData.CreateSessionEvent(GameEventType.PlayerRemove, currentSession, new PlayerRemove()
            {
                Reason =
                    targetSessionUser != null
                        ? $"{character.Name} joined {targetSessionUser.UserName}'s stream"
                        : $"{character.Name} joined another session.",

                UserId = characterUser.UserId
            });

            gameData.Add(gameEvent);
        }

        public Player GetGlobalPlayer(Guid userId)
        {
            var user = gameData.GetUser(userId);
            if (user == null)
            {
                return null;
            }

            return GetGlobalPlayer(user);
        }

        public Player GetPlayer(string userId)
        {
            var user = gameData.GetUser(userId);
            if (user == null)
            {
                return null;
            }

            return GetGlobalPlayer(user);
        }

        public Player GetPlayer(SessionToken sessionToken)
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session == null)
            {
                return null;
            }

            var user = gameData.GetUser(session.UserId);
            if (user == null)
            {
                return null;
            }

            var character = gameData.GetCharacterByUserId(user.Id);

            return character.Map(gameData, user);
        }

        public bool UpdatePlayerState(
            SessionToken sessionToken,
            CharacterStateUpdate update)
        {
            try
            {
                var player = GetCharacter(sessionToken, update.UserId);
                if (player == null)
                {
                    return false;
                }

                if (player.StateId == null)
                {
                    var state = CreateCharacterState(update);
                    gameData.Add(state);
                    player.StateId = state.Id;
                }
                else
                {
                    var state = gameData.GetState(player.StateId);
                    state.DuelOpponent = update.DuelOpponent;
                    state.Health = update.Health;
                    state.InArena = update.InArena;
                    state.InRaid = update.InRaid;
                    state.Island = update.Island;
                    state.Task = update.Task;
                    state.TaskArgument = update.TaskArgument;
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

            var character = gameData.GetCharacterByUserId(user.Id);

            return character.Map(gameData, user);
        }

        public bool UpdateSyntyAppearance(
            SessionToken token, string userId, Models.SyntyAppearance appearance)
        {
            var session = gameData.GetSession(token.SessionId);
            var character = GetCharacter(token, userId);
            if (character == null || character.UserIdLock != session.UserId)
            {
                return false;
            }

            return UpdateSyntyAppearance(userId, appearance);
        }

        public bool[] UpdateMany(SessionToken token, PlayerState[] states)
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
                var user = gameData.GetUser(state.UserId);
                if (user == null)
                {
                    logger.WriteError($"Trying to save player with userId {state.UserId}, but no user was found matching the id.");
                    results.Add(false);
                    continue;
                }

                var character = gameData.GetCharacterByUserId(user.Id);
                if (character == null)
                {
                    results.Add(false);
                    continue;
                }

                if (!integrityChecker.VerifyPlayer(gameSession.Id, character.Id, state.SyncTime))
                {
                    results.Add(false);
                    continue;
                }

                try
                {
                    if (state.Experience != null && state.Experience.Length > 0)
                    {
                        UpdateExperience(token, state.UserId, state.Experience);
                    }

                    if (state.Statistics != null && state.Statistics.Length > 0)
                    {
                        UpdateStatistics(token, state.UserId, state.Statistics);
                    }

                    EquipBestItems(character);

                    character.Revision = character.Revision.GetValueOrDefault() + 1;

                    results.Add(true);
                }
                catch
                {
                    results.Add(false);
                }
            }

            return results.ToArray();
        }

        public AddItemResult CraftItem(SessionToken token, string userId, Guid itemId)
        {
            var item = gameData.GetItem(itemId);
            if (item == null) return AddItemResult.Failed;

            var character = GetCharacter(token, userId);
            if (character == null) return AddItemResult.Failed;

            if (!integrityChecker.VerifyPlayer(token.SessionId, character.Id, 0))
                return AddItemResult.Failed;

            var resources = gameData.GetResources(character.ResourcesId);
            if (resources == null) return AddItemResult.Failed;

            var skills = gameData.GetSkills(character.SkillsId);
            if (skills == null) return AddItemResult.Failed;

            var craftingLevel = GameMath.ExperienceToLevel(skills.Crafting);
            if (item.WoodCost > resources.Wood || item.OreCost > resources.Ore || item.RequiredCraftingLevel > craftingLevel)
                return AddItemResult.Failed;

            var craftingRequirements = gameData.GetCraftingRequirements(itemId);
            foreach (var req in craftingRequirements)
            {
                var invItem = gameData.GetInventoryItem(character.Id, req.ResourceItemId);
                if (invItem == null || invItem.Amount < req.Amount)
                {
                    return AddItemResult.Failed;
                }
            }

            foreach (var req in craftingRequirements)
            {
                var invItem = gameData.GetInventoryItem(character.Id, req.ResourceItemId);
                invItem.Amount -= req.Amount;
                if (invItem.Amount == 0)
                {
                    gameData.Remove(invItem);
                }
            }

            resources.Wood -= item.WoodCost;
            resources.Ore -= item.OreCost;

            return AddItem(token, userId, itemId);
        }

        public AddItemResult AddItem(SessionToken token, string userId, Guid itemId)
        {
            var item = gameData.GetItem(itemId);
            if (item == null) return AddItemResult.Failed;

            var character = GetCharacter(token, userId);
            if (character == null) return AddItemResult.Failed;

            if (!integrityChecker.VerifyPlayer(token.SessionId, character.Id, 0))
                return AddItemResult.Failed;

            var skills = gameData.GetSkills(character.SkillsId);
            var equippedItems = gameData.FindPlayerItems(character.Id, x =>
                {
                    var xItem = gameData.GetItem(x.ItemId);
                    return x.Equipped && xItem.Type == item.Type && xItem.Category == item.Category;
                });

            var equipped = false;
            if (equippedItems.Count > 0)
            {
                var bestFirst = equippedItems.Select(x => new
                {
                    Equipped = x,
                    Item = gameData.GetItem(x.ItemId)
                }).OrderByDescending(x => GetItemValue(x.Item));

                var bestEquipped = bestFirst.First();
                if (CanEquipItem(item, skills) && IsItemBetter(item, bestEquipped.Item))
                {
                    equipped = true;
                }

                if (!equipped)
                {
                    bestEquipped.Equipped.Equipped = true;
                }

                for (var i = 1; i < equippedItems.Count; ++i)
                {
                    equippedItems[i].Equipped = false;
                }
            }

            var addNew = true;
            if (!equipped)
            {
                var inv = gameData.GetInventoryItem(character.Id, itemId);
                if (inv != null)
                {
                    inv.Amount++;
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

        public int VendorItem(
            SessionToken token,
            string userId,
            Guid itemId,
            int amount)
        {
            var player = GetCharacter(token, userId);
            if (player == null) return 0;

            if (!integrityChecker.VerifyPlayer(token.SessionId, player.Id, 0))
                return 0;

            var item = gameData.GetItem(itemId);
            if (item == null) return 0;

            var itemToVendor = gameData.GetInventoryItem(player.Id, itemId);
            if (itemToVendor == null) return 0;

            var resources = gameData.GetResources(player.ResourcesId);
            if (resources == null) return 0;

            var session = gameData.GetSession(token.SessionId);

            if (amount <= itemToVendor.Amount)
            {
                itemToVendor.Amount -= amount;
                if (itemToVendor.Amount <= 0) gameData.Remove(itemToVendor);
                resources.Coins += item.ShopSellPrice * amount;
                UpdateResources(gameData, session, player, resources);
                return amount;
            }

            gameData.Remove(itemToVendor);
            resources.Coins += itemToVendor.Amount.Value * item.ShopSellPrice;
            UpdateResources(gameData, session, player, resources);
            return (int)itemToVendor.Amount;
        }


        public int GiftItem(
            SessionToken token,
            string gifterUserId,
            string receiverUserId,
            Guid itemId,
            int amount)
        {
            var gifter = GetCharacter(token, gifterUserId);
            if (gifter == null) return 0;

            if (!integrityChecker.VerifyPlayer(token.SessionId, gifter.Id, 0))
                return 0;

            var receiver = GetCharacter(token, receiverUserId);
            if (receiver == null) return 0;

            var gift = gameData.GetInventoryItem(gifter.Id, itemId);
            if (gift == null) return 0;

            var giftedItemCount = amount;
            if (gift.Amount >= amount)
            {
                gift.Amount -= amount;
                if (gift.Amount == 0)
                {
                    gameData.Remove(gift);
                }
            }
            else
            {
                giftedItemCount = (int)gift.Amount.Value;
                gift.Amount = 0;
                gameData.Remove(gift);
            }

            var recv = gameData.GetInventoryItem(receiver.Id, itemId);
            if (recv != null)
            {
                recv.Amount += giftedItemCount;
            }
            else
            {
                gameData.Add(new InventoryItem
                {
                    Id = Guid.NewGuid(),
                    CharacterId = receiver.Id,
                    Amount = giftedItemCount,
                    Equipped = false,
                    ItemId = itemId
                });
            }

            gameData.Add(gameData.CreateSessionEvent(GameEventType.ItemAdd, gameData.GetSession(token.SessionId), new ItemAdd
            {
                UserId = receiverUserId,
                Amount = giftedItemCount,
                ItemId = itemId
            }));


            EquipBestItems(receiver);

            return giftedItemCount;
        }

        public void EquipItem(Character character, InventoryItem item)
        {
            if (item.Amount > 1)
            {
                --item.Amount;

                item = new InventoryItem
                {
                    Id = Guid.NewGuid(),
                    CharacterId = character.Id,
                    Amount = 1,
                    Equipped = true,
                    ItemId = item.ItemId,
                };
                gameData.Add(item);
            }
            else
            {
                item.Equipped = true;
            }
        }

        public bool EquipItem(SessionToken token, string userId, Guid itemId)
        {
            var character = GetCharacter(token, userId);
            if (character == null) return false;

            var item = gameData.GetItem(itemId);
            if (item == null) return false;

            var invItem = gameData.GetInventoryItem(character.Id, itemId);

            var skills = gameData.GetSkills(character.SkillsId);
            if (invItem == null || !CanEquipItem(gameData.GetItem(invItem.ItemId), skills))
                return false;

            if (invItem.Amount > 1)
            {
                --invItem.Amount;

                invItem = new InventoryItem
                {
                    Id = Guid.NewGuid(),
                    CharacterId = character.Id,
                    Amount = 1,
                    Equipped = true,
                    ItemId = invItem.ItemId,
                };
                gameData.Add(invItem);
            }
            else
            {
                invItem.Equipped = true;
            }

            InventoryItem invItemEq = gameData.FindPlayerItem(character.Id, x =>
            {
                var xItem = gameData.GetItem(x.ItemId);
                return xItem.Type == item.Type && xItem.Category == item.Category && x.Equipped;
            });

            if (invItemEq != null)
            {
                var stack = gameData.GetInventoryItem(character.Id, invItemEq.ItemId);

                if (stack != null)
                {
                    ++stack.Amount;
                    gameData.Remove(invItemEq);
                }
                else
                {
                    invItemEq.Equipped = false;
                }
            }

            return true;
        }

        public bool UnEquipItem(SessionToken token, string userId, Guid itemId)
        {
            var character = GetCharacter(token, userId);
            if (character == null) return false;

            var invItem = gameData.GetEquippedItem(character.Id, itemId);
            if (invItem == null) return false;

            var stack = gameData.GetInventoryItem(character.Id, itemId);
            if (stack != null)
            {
                ++stack.Amount;
                gameData.Remove(invItem);
            }
            else
            {
                invItem.Equipped = false;
            }
            return true;
        }

        public bool ToggleHelmet(SessionToken token, string userId)
        {
            var character = GetCharacter(token, userId);
            if (character == null) return false;

            var appearance = gameData.GetAppearance(character.SyntyAppearanceId);
            if (appearance == null) return false;

            appearance.HelmetVisible = !appearance.HelmetVisible;
            return true;
        }

        public ItemCollection GetEquippedItems(SessionToken token, string userId)
        {
            var itemCollection = new ItemCollection();
            var character = GetCharacter(token, userId);
            if (character == null) return itemCollection;

            var items = gameData.GetEquippedItems(character.Id);
            foreach (var inv in items)
            {
                itemCollection.Add(ModelMapper.Map(gameData, gameData.GetItem(inv.ItemId)));
            }

            return itemCollection;
        }

        public ItemCollection GetAllItems(SessionToken token, string userId)
        {
            var itemCollection = new ItemCollection();
            var character = GetCharacter(token, userId);
            if (character == null) return itemCollection;

            var items = gameData.GetAllPlayerItems(character.Id);
            if (items == null || items.Count == 0)
                return itemCollection;

            foreach (var inv in items)
            {
                var item = gameData.GetItem(inv.ItemId);
                if (item != null)
                {
                    itemCollection.Add(ModelMapper.Map(gameData, item));
                }
            }

            return itemCollection;
        }

        public IReadOnlyList<Player> GetPlayers()
        {
            var users = gameData.GetUsers();
            return users.Select(x => x.Map(gameData, gameData.GetCharacterByUserId(x.Id))).ToList();
        }

        public bool UpdateStatistics(SessionToken token, string userId, decimal[] statistics)
        {
            var character = GetCharacter(token, userId);
            if (character == null) return false;
            if (!AcquiredUserLock(token, character)) return false;

            var index = 0;

            var characterStatistics = gameData.GetStatistics(character.StatisticsId);

            characterStatistics.RaidsWon += (int)statistics[index++];
            characterStatistics.RaidsLost += (int)statistics[index++];
            characterStatistics.RaidsJoined += (int)statistics[index++];

            characterStatistics.DuelsWon += (int)statistics[index++];
            characterStatistics.DuelsLost += (int)statistics[index++];

            characterStatistics.PlayersKilled += (int)statistics[index++];
            characterStatistics.EnemiesKilled += (int)statistics[index++];

            characterStatistics.ArenaFightsJoined += (int)statistics[index++];
            characterStatistics.ArenaFightsWon += (int)statistics[index++];

            characterStatistics.TotalDamageDone += (long)statistics[index++];
            characterStatistics.TotalDamageTaken += (long)statistics[index++];
            characterStatistics.DeathCount += (int)statistics[index++];

            characterStatistics.TotalWoodCollected += statistics[index++];
            characterStatistics.TotalOreCollected += statistics[index++];
            characterStatistics.TotalFishCollected += statistics[index++];
            characterStatistics.TotalWheatCollected += statistics[index++];

            characterStatistics.CraftedWeapons += (int)statistics[index++];
            characterStatistics.CraftedArmors += (int)statistics[index++];
            characterStatistics.CraftedPotions += (int)statistics[index++];
            characterStatistics.CraftedRings += (int)statistics[index++];
            characterStatistics.CraftedAmulets += (int)statistics[index++];

            characterStatistics.CookedFood += (int)statistics[index++];

            characterStatistics.ConsumedPotions += (int)statistics[index++];
            characterStatistics.ConsumedFood += (int)statistics[index++];

            characterStatistics.TotalTreesCutDown += (long)statistics[index];
            return false;
        }

        public bool UpdateSyntyAppearance(string userId, Models.SyntyAppearance appearance)
        {
            try
            {
                var user = gameData.GetUser(userId);
                if (user == null) return false;

                var character = gameData.GetCharacterByUserId(user.Id);
                if (character == null) return false;

                UpdateCharacterAppearance(appearance, character);

                var sessionOwnerUserId = character.UserIdLock.GetValueOrDefault();
                var gameSession = gameData.GetUserSession(sessionOwnerUserId);

                if (gameSession != null)
                {
                    var gameEvent = gameData.CreateSessionEvent(GameEventType.PlayerAppearance, gameSession, new SyntyAppearanceUpdate
                    {
                        UserId = userId,
                        Value = appearance
                    });

                    gameData.Add(gameEvent);
                }

                return true;
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

        private Player GetGlobalPlayer(User user)
        {
            var character = gameData.FindCharacter(x => x.UserId == user.Id && !x.Local);
            return character.Map(gameData, user);
        }

        private void UpdateCharacterAppearance(Models.SyntyAppearance appearance, Character character)
        {
            var characterAppearance = gameData.GetAppearance(character.SyntyAppearanceId);
            //DataMapper.RefMap(appearance, characterAppearance, nameof(appearance.Id));

            characterAppearance.BeardColor = appearance.BeardColor;
            characterAppearance.Eyebrows = appearance.Eyebrows;
            characterAppearance.EyeColor = appearance.EyeColor;
            characterAppearance.FacialHair = appearance.FacialHair;
            characterAppearance.Gender = (DataModels.Gender)(int)appearance.Gender;
            characterAppearance.Hair = appearance.Hair;
            characterAppearance.SkinColor = appearance.SkinColor;
            characterAppearance.StubbleColor = appearance.StubbleColor;
            characterAppearance.WarPaintColor = appearance.WarPaintColor;
            characterAppearance.Head = appearance.Head;
            characterAppearance.HairColor = appearance.HairColor;
        }

        public bool UpdateExperience(
            SessionToken token,
            string userId,
            decimal[] experience)
        {
            try
            {
                var gameSession = gameData.GetSession(token.SessionId);
                var character = GetCharacter(token, userId);
                if (character == null) return false;
                if (!AcquiredUserLock(token, character)) return false;

                var sessionOwner = gameData.GetUser(gameSession.UserId);
                var expLimit = sessionOwner.IsAdmin.GetValueOrDefault() ? 5000 : 50;

                var characterSessionState = gameData.GetCharacterSessionState(token.SessionId, character.Id);
                var gains = characterSessionState.ExpGain;

                var skills = gameData.GetSkills(character.SkillsId);
                var skillIndex = 0;

                skills.Attack += GetDelta(expLimit, gains.Attack, skills.Attack, experience[skillIndex++]);
                skills.Defense += GetDelta(expLimit, gains.Defense, skills.Defense, experience[skillIndex++]);
                skills.Strength += GetDelta(expLimit, gains.Strength, skills.Strength, experience[skillIndex++]);
                skills.Health += GetDelta(expLimit, gains.Health, skills.Health, experience[skillIndex++]);
                skills.Woodcutting += GetDelta(expLimit, gains.Woodcutting, skills.Woodcutting, experience[skillIndex++]);
                skills.Fishing += GetDelta(expLimit, gains.Fishing, skills.Fishing, experience[skillIndex++]);
                skills.Mining += GetDelta(expLimit, gains.Mining, skills.Mining, experience[skillIndex++]);
                skills.Crafting += GetDelta(expLimit, gains.Crafting, skills.Crafting, experience[skillIndex++]);
                skills.Cooking += GetDelta(expLimit, gains.Cooking, skills.Cooking, experience[skillIndex++]);
                skills.Farming += GetDelta(expLimit, gains.Farming, skills.Farming, experience[skillIndex++]);
                skills.Slayer += GetDelta(expLimit, gains.Slayer, skills.Slayer, experience[skillIndex++]);
                skills.Magic += GetDelta(expLimit, gains.Magic, skills.Magic, experience[skillIndex++]);
                skills.Ranged += GetDelta(expLimit, gains.Ranged, skills.Ranged, experience[skillIndex++]);
                skills.Sailing += GetDelta(expLimit, gains.Sailing, skills.Sailing, experience[skillIndex++]);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public void EquipBestItems(Character character)
        {
            var equippedPetInventoryItem = gameData.GetEquippedItem(character.Id, ItemCategory.Pet);

            UnequipAllItems(character);

            var skills = gameData.GetSkills(character.SkillsId);
            var inventoryItems = gameData
                .GetInventoryItems(character.Id)
                .Select(x => new { InventoryItem = x, Item = gameData.GetItem(x.ItemId) })
                .Where(x => CanEquipItem(x.Item, skills))
                .OrderByDescending(x => GetItemValue(x.Item))
                .ToList();

            var weaponToEquip = inventoryItems.FirstOrDefault(x => x.Item.Category == (int)ItemCategory.Weapon);
            if (weaponToEquip != null)
            {
                EquipItem(character, weaponToEquip.InventoryItem);
            }

            InventoryItem equippedPet = null;
            if (equippedPetInventoryItem != null)
            {
                equippedPet = gameData.GetInventoryItem(character.Id, equippedPetInventoryItem.ItemId);
                if (equippedPet != null)
                {
                    EquipItem(character, equippedPet);
                }
            }

            foreach (var itemGroup in inventoryItems
                .Where(x => x.Item.Category != (int)ItemCategory.Weapon && x.Item.Category != (int)ItemCategory.Pet)
                .GroupBy(x => x.Item.Type))
            {
                var itemToEquip = itemGroup
                    .OrderByDescending(x => GetItemValue(x.Item))
                    .FirstOrDefault();

                if (itemToEquip != null)
                {
                    EquipItem(character, itemToEquip.InventoryItem);
                }
            }
        }

        private void UnequipAllItems(Character character)
        {
            var allEquippedItems = gameData.GetEquippedItems(character.Id);
            foreach (var equipped in allEquippedItems)
            {
                var stack = gameData.GetInventoryItem(character.Id, equipped.ItemId);
                if (stack != null)
                {
                    stack.Amount += equipped.Amount;
                    gameData.Remove(equipped);
                }
                else
                {
                    equipped.Equipped = false;
                }
            }
        }

        public bool UpdateResources(
            SessionToken token,
            string userId,
            decimal[] resources)
        {
            try
            {
                var index = 0;

                var character = GetCharacter(token, userId);
                if (character == null) return false;
                if (!AcquiredUserLock(token, character)) return false;

                var characterResources = gameData.GetResources(character.ResourcesId);
                characterResources.Wood += resources[index++];
                characterResources.Fish += resources[index++];
                characterResources.Ore += resources[index++];
                characterResources.Wheat += resources[index++];
                characterResources.Coins += resources[index++];
                characterResources.Magic += resources[index++];
                characterResources.Arrows += resources[index];
                return true;
            }
            catch
            {
                return false;
            }
        }

        private Player CreatePlayer(
            DataModels.GameSession session, string userId, string userName)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                UserName = userName,
                Created = DateTime.UtcNow
            };

            gameData.Add(user);
            return CreatePlayer(session, user);
        }

        private Player CreatePlayer(DataModels.GameSession session, User user)
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
            character.ResourcesId = resources.Id;
            character.AppearanceId = appearance.Id;
            character.StatisticsId = statistics.Id;
            character.SkillsId = skills.Id;
            character.UserIdLock = session?.UserId;
            character.LastUsed = DateTime.UtcNow;
            gameData.Add(character);
            return character.Map(gameData, user);
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

        private Character GetCharacter(SessionToken token, string userId)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null) return null;

            var user = gameData.GetUser(userId);
            if (user == null) return null;

            return gameData.GetCharacterByUserId(user.Id);
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

        private void UpdateResources(IGameData gameData, DataModels.GameSession session, Character character, DataModels.Resources resources)
        {
            var user = gameData.GetUser(character.UserId);
            var gameEvent = gameData.CreateSessionEvent(GameEventType.ResourceUpdate, session,
                new ResourceUpdate
                {
                    UserId = user.UserId,
                    FishAmount = resources.Fish,
                    OreAmount = resources.Ore,
                    WheatAmount = resources.Wheat,
                    WoodAmount = resources.Wood,
                    CoinsAmount = resources.Coins
                });

            gameData.Add(gameEvent);
        }

        private static DataModels.SyntyAppearance GenerateRandomSyntyAppearance()
        {
            var gender = Utility.Random<Gender>();
            var skinColor = GetHexColor(Utility.Random<DataModels.SkinColor>());
            var hairColor = GetHexColor(Utility.Random<DataModels.HairColor>());
            var beardColor = GetHexColor(Utility.Random<DataModels.HairColor>());
            return new DataModels.SyntyAppearance
            {
                Id = Guid.NewGuid(),
                Gender = gender,
                SkinColor = skinColor,
                HairColor = hairColor,
                BeardColor = beardColor,
                StubbleColor = skinColor,
                WarPaintColor = hairColor,
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
                StubbleColor = GetHexColor(appearance.BeardColor),
                WarPaintColor = GetHexColor(appearance.BeardColor),
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
        public bool AcquiredUserLock(SessionToken token, Character character)
        {
            var session = gameData.GetSession(token.SessionId);
            return character.UserIdLock == session.UserId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AcquiredUserLock(DataModels.GameSession session, Character character)
        {
            return character.UserIdLock == session.UserId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CanEquipItem(Item item, Skills skills)
        {
            return item.Category != (int)ItemCategory.Resource &&
                   item.RequiredDefenseLevel <= GameMath.ExperienceToLevel(skills.Defense) &&
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static decimal GetDelta(decimal expMultiplierLimit, ExpGain expGain, decimal currentExp, decimal newExp)
        {
            var delta = GetDelta(currentExp, newExp);
            expGain.AddExperience(delta);

#warning disabled integrity check (XP)

            // TODO(Zerratar): enable it again in the future.

            //if (expMultiplierLimit >= 500)
            //    return delta;

            //if (expGain.ExpPerHour >= 25_000_000)
            //    return 0;

            return delta;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static decimal GetDelta(decimal currentExp, decimal newExp)
        {
            return Math.Max(currentExp, newExp) - currentExp;
        }
    }
}
