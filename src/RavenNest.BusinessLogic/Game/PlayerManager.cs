using System;
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
using ItemType = RavenNest.DataModels.ItemType;
using Resources = RavenNest.DataModels.Resources;
using Skills = RavenNest.DataModels.Skills;
using Statistics = RavenNest.DataModels.Statistics;

namespace RavenNest.BusinessLogic.Game
{
    public class PlayerManager : IPlayerManager
    {
        private readonly IGameData gameData;

        public PlayerManager(IGameData gameData)
        {
            this.gameData = gameData;
        }

        public Player CreatePlayerIfNotExists(string userId, string userName)
        {
            var player = GetGlobalPlayer(userId);
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
                return CreatePlayer(session, userId, userName);
            }

            var character = gameData.GetCharacterByUserId(user.Id);
            //var character = await db.Character
            //    .Include(x => x.User)
            //    .Include(x => x.InventoryItem) //.ThenInclude(x => x.Item)
            //    .Include(x => x.Appearance)
            //    .Include(x => x.SyntyAppearance)
            //    .Include(x => x.Resources)
            //    .Include(x => x.Skills)
            //    .Include(x => x.Statistics)
            //    .FirstOrDefaultAsync(x =>
            //        x.UserId == user.Id &&
            //        ((session.Local && x.Local && x.OriginUserId == session.UserId) ||
            //         !session.Local && !x.Local));

            if (character == null)
            {
                return CreatePlayer(session, user);
            }

            //if (character.SyntyAppearance == null && character.Appearance != null)
            //{
            //    var syntyApp = GenerateSyntyAppearance(character.Appearance);
            //    gameData.Add(syntyApp);
            //    character.SyntyAppearanceId = syntyApp.Id;
            //    character.SyntyAppearance = syntyApp;
            //}

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

        private void TryRemovePlayerFromPreviousSession(Character character, GameSession joiningSession)
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

        public Player GetGlobalPlayer(string userId)
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
                // add a temporary throttle to avoid spamming server with player state updates.
                // THIS NEEDS TO BE BULKUPDATED!!!!!

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
                var character = gameData.GetCharacterByUserId(user.Id);
                if (character == null)
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

                    if (state.Resources != null && state.Resources.Length > 0)
                    {
                        UpdateResources(token, state.UserId, state.Resources);
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

        public AddItemResult AddItem(SessionToken token, string userId, Guid itemId)
        {
            var item = gameData.GetItem(itemId);
            if (item == null) return AddItemResult.Failed;

            var character = GetCharacter(token, userId);
            if (character == null) return AddItemResult.Failed;

            //var characterSession = await GetActiveCharacterSessionAsync(token, userId);
            //if (characterSession == null) return AddItemResult.Failed;
            //var character = characterSession.Character;
            var skills = gameData.GetSkills(character.SkillsId);
            var equippedItems = gameData.FindPlayerItems(character.Id, x =>
                {
                    var xItem = gameData.GetItem(x.ItemId);
                    return x.Equipped && xItem.Type == item.Type && xItem.Category == item.Category;
                });

            //var equippedItems = await db.InventoryItem
            //        .Include(x => x.Item)
            //        .Where(x =>
            //            x.CharacterId == character.Id &&
            //            )
            //        .ToListAsync();

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
                //var inv = await db.InventoryItem
                //    .FirstOrDefaultAsync(x => x.ItemId == itemId && !x.Equipped && x.CharacterId == character.Id);
                if (inv != null)
                {
                    inv.Amount += 1;
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

        public bool GiftItem(
            SessionToken token,
            string gifterUserId,
            string receiverUserId,
            Guid itemId)
        {
            return false;
        }

        public bool GiftResources(
            SessionToken token,
            string giftUserId,
            string receiverUserId,
            string resource,
            long amount)
        {
            return false;
        }

        public bool EquipItem(SessionToken token, string userId, Guid itemId)
        {
            var character = GetCharacter(token, userId);
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

            var skills = gameData.GetSkills(character.SkillsId);
            if (invItem == null || !CanEquipItem(gameData.GetItem(invItem.ItemId), skills))
                return false;

            if (invItem.Amount > 1)
            {
                invItem.Amount = invItem.Amount - 1;
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
            }
            else
            {
                invItem.Equipped = false;
            }
            return true;
        }

        public ItemCollection GetEquippedItems(SessionToken token, string userId)
        {
            var itemCollection = new ItemCollection();
            var character = GetCharacter(token, userId);
            if (character == null) return itemCollection;
            /*
                .Include(x => x.Item)
                .Where(x => x.CharacterId == character.Id && x.Equipped)            
             */

            var items = gameData.GetEquippedItems(character.Id);
            foreach (var inv in items)
            {
                itemCollection.Add(ModelMapper.Map(gameData.GetItem(inv.ItemId)));
            }

            return itemCollection;
        }

        public ItemCollection GetAllItems(SessionToken token, string userId)
        {
            var itemCollection = new ItemCollection();
            var character = GetCharacter(token, userId);
            if (character == null) return itemCollection;

            var items = gameData.GetAllPlayerItems(character.Id);
            foreach (var inv in items)
            {
                itemCollection.Add(ModelMapper.Map(gameData.GetItem(inv.ItemId)));
            }

            return null;
        }

        public IReadOnlyList<Player> GetPlayers()
        {
            var characters = gameData.GetCharacters(x => !x.Local);
            return characters.Select(x => x.Map(gameData, gameData.GetUser(x.UserId))).ToList();
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
            DataMapper.RefMap(appearance, characterAppearance, nameof(appearance.Id));
        }

        public bool UpdateExperience(
            SessionToken token,
            string userId,
            decimal[] experience)
        {
            try
            {
                var character = GetCharacter(token, userId);
                if (character == null) return false;
                if (!AcquiredUserLock(token, character)) return false;

                var skills = gameData.GetSkills(character.SkillsId);
                var skillIndex = 0;
                var attackExperience = Math.Max(skills.Attack, experience[skillIndex++]);
                var defenseExperience = Math.Max(skills.Defense, experience[skillIndex++]);

                skills.Attack = attackExperience;
                skills.Defense = defenseExperience;
                skills.Strength = Math.Max(skills.Strength, experience[skillIndex++]);
                skills.Health = Math.Max(skills.Health, experience[skillIndex++]);
                skills.Woodcutting = Math.Max(skills.Woodcutting, experience[skillIndex++]);
                skills.Fishing = Math.Max(skills.Fishing, experience[skillIndex++]);
                skills.Mining = Math.Max(skills.Mining, experience[skillIndex++]);
                skills.Crafting = Math.Max(skills.Crafting, experience[skillIndex++]);
                skills.Cooking = Math.Max(skills.Cooking, experience[skillIndex++]);
                skills.Farming = Math.Max(skills.Farming, experience[skillIndex++]);

                if (experience.Length > 10) skills.Slayer = Math.Max(skills.Slayer, experience[skillIndex++]);
                if (experience.Length > 11) skills.Magic = Math.Max(skills.Magic, experience[skillIndex++]);
                if (experience.Length > 12) skills.Ranged = Math.Max(skills.Ranged, experience[skillIndex++]);
                if (experience.Length > 13) skills.Sailing = Math.Max(skills.Sailing, experience[skillIndex++]);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void EquipBestItems(Character character)
        {
            // TODO(Zerratar): remove the double item lookup (gameData.GetItem(...))
            var skills = gameData.GetSkills(character.SkillsId);
            var weapons = gameData.GetEquippedItems(character.Id)
                .Select(x => new { InventoryItem = x, Item = gameData.GetItem(x.ItemId) })
                .Where(x => x.Item.Category == (int)ItemCategory.Weapon)
                .OrderByDescending(x => GetItemValue(x.Item))
                .ToList();

            if (weapons.Count > 0)
            {
                var weaponToEquip = weapons.FirstOrDefault(x => CanEquipItem(x.Item, skills));
                if (weaponToEquip != null)
                {
                    weaponToEquip.InventoryItem.Equipped = true;
                }

                foreach (var weapon in weapons.Where(x => x != weaponToEquip))
                {
                    weapon.InventoryItem.Equipped = false;
                }

            }

            var items = gameData.FindPlayerItems(character.Id, x =>
            {
                var item = gameData.GetItem(x.ItemId);
                return item.Category != (int)ItemCategory.Weapon;
            });

            var inventoryItems = gameData.GetAllPlayerItems(character.Id)
                .Select(x => new { InventoryItem = x, Item = gameData.GetItem(x.ItemId) });

            foreach (var itemGroup in inventoryItems
                .Where(x => x.Item.Category != (int)ItemCategory.Weapon)
                .GroupBy(x => x.Item.Type))
            {
                var itemToEquip = itemGroup
                    .OrderByDescending(x => GetItemValue(x.Item))
                    .FirstOrDefault(x => CanEquipItem(x.Item, skills));

                // ensure we don't change equipped pet
                if (itemGroup.Key == (int)ItemType.Pet)
                {
                    var alreadyEquipped = itemGroup.FirstOrDefault(x => x.InventoryItem.Equipped);
                    if (alreadyEquipped != null)
                    {
                        itemToEquip = alreadyEquipped;
                    }
                }

                if (itemToEquip != null)
                {
                    itemToEquip.InventoryItem.Equipped = true;

                    if (itemToEquip.InventoryItem.Amount > 1)
                    {
                        var diff = itemToEquip.InventoryItem.Amount - 1;

                        var inventoryItem = inventoryItems
                            .FirstOrDefault(x => x.Item.Id == itemToEquip.Item.Id && !x.InventoryItem.Equipped);

                        if (inventoryItem == null)
                        {
                            gameData.Add(new InventoryItem
                            {
                                Id = Guid.NewGuid(),
                                ItemId = itemToEquip.Item.Id,
                                CharacterId = itemToEquip.InventoryItem.CharacterId,
                                Equipped = false,
                                Amount = diff
                            });
                        }
                        else
                        {
                            inventoryItem.InventoryItem.Amount += diff;
                        }

                        itemToEquip.InventoryItem.Amount = 1;
                    }

                }

                foreach (var item in itemGroup.Where(x => x != itemToEquip))
                {
                    item.InventoryItem.Equipped = false;
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

                // UpdateDeltaClamped(resource, 0, resourceAmount)
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
            return CreatePlayer(session, user);
        }

        private Player CreatePlayer(GameSession session, User user)
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
        public bool AcquiredUserLock(SessionToken token, Character character)
        {
            var session = gameData.GetSession(token.SessionId);
            return character.UserIdLock == session.UserId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AcquiredUserLock(GameSession session, Character character)
        {
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
