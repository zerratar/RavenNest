using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Net;
using RavenNest.DataModels;
using RavenNest.Models;

using Appearance = RavenNest.DataModels.Appearance;
using Gender = RavenNest.DataModels.Gender;
using Item = RavenNest.DataModels.Item;
using Resources = RavenNest.DataModels.Resources;
using Skills = RavenNest.DataModels.Skills;
using Statistics = RavenNest.DataModels.Statistics;

namespace RavenNest.BusinessLogic.Game
{
    public class PlayerManager : IPlayerManager
    {
        private const int MaxCharacterCount = 3;
        private readonly ILogger logger;
        private readonly IPlayerInventoryProvider inventoryProvider;
        private readonly IGameData gameData;
        private readonly IIntegrityChecker integrityChecker;

        public PlayerManager(
            ILogger<PlayerManager> logger,
            IPlayerInventoryProvider inventoryProvider,
            IGameData gameData,
            IIntegrityChecker integrityChecker)
        {
            this.logger = logger;
            this.inventoryProvider = inventoryProvider;
            this.gameData = gameData;
            this.integrityChecker = integrityChecker;
        }

        public Player CreatePlayerIfNotExists(string userId, string userName, string identifier)
        {
            var player = GetPlayer(userId, identifier);
            if (player != null) return player;
            return CreatePlayer(userId, userName, identifier);
        }

        public Player CreatePlayer(string userId, string userName, string identifier)
        {
            return CreatePlayer(null, userId, userName, identifier);
        }

        public PlayerJoinResult AddPlayer(SessionToken token, string userId, string userName, string identifier = null)
        {
            var result = new PlayerJoinResult();
            var session = gameData.GetSession(token.SessionId);
            if (session == null || session.Status != (int)SessionStatus.Active)
            {
                result.ErrorMessage = "Session is unavailable";
                return result;
            }

            var user = gameData.GetUser(userId);
            if (user == null)
            {
                result.Success = true;
                result.Player = CreatePlayer(session, userId, userName, identifier);
                return result;
            }

            if (string.IsNullOrEmpty(user.UserName))
            {
                user.UserName = userName;
                user.DisplayName = userName;
            }

            var character = gameData.GetCharacterByUserId(user.Id, identifier);
            if (character == null)
            {
                var player = CreatePlayer(session, user, identifier);
                if (player != null)
                {
                    result.Success = true;
                    result.Player = player;
                }
                else
                {
                    result.ErrorMessage = "Maximum character count of " + MaxCharacterCount + " exceeded.";
                }
                return result;
            }

            if (string.IsNullOrEmpty(character.Name))
            {
                character.Name = userName;
            }

            result.Player = AddPlayer(session, user, character);
            result.Success = true;
            return result;
        }
        public Player AddPlayer(SessionToken token, Guid characterId)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null || session.Status != (int)SessionStatus.Active)
            {
                return null;
            }
            var character = gameData.GetCharacter(characterId);
            var user = gameData.GetUser(character.UserId);
            return AddPlayer(session, user, character);
        }
        public Player AddPlayer(DataModels.GameSession session, Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            var user = gameData.GetUser(character.UserId);
            return AddPlayer(session, user, character);
        }

        private Player AddPlayer(DataModels.GameSession session, User user, Character character)
        {

            // check if we need to remove the player from
            // their active session.

            var sessionChars = gameData.GetSessionCharacters(session);
            var charactersInSession = sessionChars.Where(x => x.UserId == user.Id).ToList();

            foreach (var cs in charactersInSession)
            {
                cs.UserIdLock = null;
            }

            if (character.UserIdLock != null)
            {
                TryRemovePlayerFromPreviousSession(character, session);
            }

            var rejoin = character.UserIdLock == session.UserId;
            character.UserIdLock = session.UserId;
            character.LastUsed = DateTime.UtcNow;

            return character.Map(gameData, user, rejoin);
        }

        private void TryRemovePlayerFromPreviousSession(Character character, DataModels.GameSession joiningSession)
        {
            var userToRemove = gameData.GetUser(character.UserId);
            if (userToRemove == null)
                return;

            var currentSession = gameData.GetUserSession(character.UserIdLock.GetValueOrDefault());
            if (currentSession == null || currentSession.Id == joiningSession.Id || currentSession.UserId == joiningSession.UserId)
                return;

            var targetSessionUser = gameData.GetUser(joiningSession.UserId);
            var characterUser = gameData.GetUser(character.UserId);
            var gameEvent = gameData.CreateSessionEvent(
                GameEventType.PlayerRemove,
                currentSession,
                new PlayerRemove()
                {
                    Reason =
                    targetSessionUser != null
                        ? $"{character.Name} joined {targetSessionUser.UserName}'s stream"
                        : $"{character.Name} joined another session.",

                    UserId = characterUser.UserId
                });

            gameData.Add(gameEvent);
        }

        public Player GetGlobalPlayer(Guid userId, string identifier)
        {
            var user = gameData.GetUser(userId);
            if (user == null)
            {
                return null;
            }

            return GetPlayerByUser(user, identifier);
        }

        public PlayerExtended GetGlobalPlayerExtended(Guid userId, string identifier)
        {
            var user = gameData.GetUser(userId);
            if (user == null)
            {
                return null;
            }

            return GetPlayerExtended(user, identifier);
        }

        public PlayerExtended GetPlayerExtended(string userId, string identifier)
        {
            var user = gameData.GetUser(userId);
            if (user == null)
            {
                return null;
            }

            return GetPlayerExtended(user, identifier);
        }

        public Player GetPlayer(string userId, string identifier)
        {
            var user = gameData.GetUser(userId);
            if (user == null)
            {
                return null;
            }

            return GetPlayerByUser(user, identifier);
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

            var sessionCharacters = gameData.GetSessionCharacters(session);
            var character = sessionCharacters.FirstOrDefault(x => x.UserId == user.Id);
            if (character == null)
            {
                character = gameData.GetCharacterByUserId(user.Id, "1");
            }

            return character.Map(gameData, user);
        }

        public bool UpdatePlayerState(
            SessionToken sessionToken,
            CharacterStateUpdate update)
        {
            try
            {
                var player = update.CharacterId != null
                ? gameData.GetCharacter(update.CharacterId)
                : GetCharacter(sessionToken, update.UserId);

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
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                return false;
            }
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

            var sessionCharacters = gameData.GetSessionCharacters(session);
            var character = sessionCharacters.FirstOrDefault(x => x.UserId == user.Id);
            if (character == null)
            {
                character = gameData.GetCharacterByUserId(user.Id, "1");
            }

            return character.Map(gameData, user);
        }

        public bool UpdateAppearance(
            SessionToken token, string userId, Models.SyntyAppearance appearance)
        {
            var session = gameData.GetSession(token.SessionId);
            var character = GetCharacter(token, userId);
            if (character == null || character.UserIdLock != session.UserId)
            {
                return false;
            }

            return UpdateAppearance(userId, character.Identifier ?? character.CharacterIndex.ToString(), appearance);
        }

        public bool UpdateAppearance(
            AuthToken token,
            string userId,
            string identifier,
            Models.SyntyAppearance appearance)
        {

            var character = gameData.GetCharacterByUserId(token.UserId, identifier);
            var control = gameData.GetCharacterByUserId(userId, identifier);
            if (character == null || control.Id != character.Id)
            {
                return false;
            }

            return UpdateAppearance(userId, identifier, appearance);
        }

        public bool[] UpdateMany(SessionToken token, PlayerState[] states)
        {
            var results = new List<bool>();
            var gameSession = gameData.GetSession(token.SessionId);
            if (gameSession == null)
            {
                return Enumerable.Range(0, states.Length).Select(x => false).ToArray();
            }

            //var sessionPlayers = gameData.GetSessionCharacters(gameSession);
            foreach (var state in states)
            {
                var user = gameData.GetUser(state.UserId);
                if (user == null)
                {
                    logger.LogError($"Saving failed for player with userId {state.UserId}, no user was found matching the id.");
                    results.Add(false);
                    continue;
                }

                var character = gameData.GetCharacter(state.CharacterId);//sessionPlayers.FirstOrDefault(x => x.UserId == user.Id);
                //var character = gameData.GetCharacterByUserId(user.Id);
                if (character == null)
                {
                    logger.LogError($"Saving failed for player with userId {state.UserId}, no character was found matching the id in the session.");
                    results.Add(false);
                    continue;
                }

                if (!integrityChecker.VerifyPlayer(gameSession.Id, character.Id, state.SyncTime))
                {
                    logger.LogError($"Saving failed for player with userId {state.UserId}, INTEGRITY CHECK!!.");
                    results.Add(false);
                    continue;
                }

                try
                {
                    if (state.Experience != null && state.Experience.Length > 0)
                    {
                        UpdateExperience(token, state.UserId, state.Level, state.Experience, state.CharacterId);
                    }

                    if (state.Statistics != null && state.Statistics.Length > 0)
                    {
                        UpdateStatistics(token, state.UserId, state.Statistics, state.CharacterId);
                    }

                    EquipBestItems(character);

                    character.Revision = character.Revision.GetValueOrDefault() + 1;

                    results.Add(true);
                }
                catch (Exception exc)
                {
                    logger.LogError("Failed updating many: " + exc);
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

            var craftingLevel = skills.CraftingLevel;
            if (item.WoodCost > resources.Wood || item.OreCost > resources.Ore || item.RequiredCraftingLevel > craftingLevel)
                return AddItemResult.Failed;

            var craftingRequirements = gameData.GetCraftingRequirements(itemId);
            var inventory = inventoryProvider.Get(character.Id);
            foreach (var req in craftingRequirements)
            {
                var invItem = inventory.GetItem(req.ResourceItemId);
                if (invItem.IsNull() || invItem.Amount < req.Amount)
                {
                    return AddItemResult.Failed;
                }
            }

            foreach (var req in craftingRequirements)
            {
                inventory.RemoveItem(req.ResourceItemId, req.Amount);
            }

            resources.Wood -= item.WoodCost;
            resources.Ore -= item.OreCost;

            return AddItem(token, userId, itemId);
        }

        public AddItemResult AddItem(SessionToken token, string userId, Guid itemId)
        {
            var item = gameData.GetItem(itemId);
            if (item == null)
                return AddItemResult.Failed;

            var character = GetCharacter(token, userId);
            if (character == null)
                return AddItemResult.Failed;

            if (!integrityChecker.VerifyPlayer(token.SessionId, character.Id, 0))
                return AddItemResult.Failed;

            var session = gameData.GetSession(token.SessionId);
            if (session == null)
                return AddItemResult.Failed;

            var sessionOwner = gameData.GetUser(session.UserId);
            if (sessionOwner == null)
                return AddItemResult.Failed;

            string tag = null;
            if (item.Category == (int)DataModels.ItemCategory.StreamerToken)
                tag = sessionOwner.UserId;

            var inventory = inventoryProvider.Get(character.Id);
            inventory.AddItem(itemId, tag: tag);
            inventory.EquipBestItems();

            return inventory.GetEquippedItem(itemId).IsNotNull()
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
            if (item == null || item.Category == (int)DataModels.ItemCategory.StreamerToken)
                return 0;

            var inventory = inventoryProvider.Get(player.Id);

            var itemToVendor = inventory.GetItem(itemId);
            if (itemToVendor.IsNull()) return 0;

            var resources = gameData.GetResources(player.ResourcesId);
            if (resources == null) return 0;

            var session = gameData.GetSession(token.SessionId);

            if (amount <= itemToVendor.Amount)
            {
                inventory.RemoveItem(itemToVendor.ItemId, amount);
                resources.Coins += item.ShopSellPrice * amount;
                UpdateResources(gameData, session, player, resources);
                return amount;
            }

            inventory.RemoveStack(itemToVendor.Id);

            resources.Coins += itemToVendor.Amount * item.ShopSellPrice;
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

            var item = gameData.GetItem(itemId);
            if (item == null || item.Category == (int)DataModels.ItemCategory.StreamerToken)
                return 0;

            var inventory = inventoryProvider.Get(gifter.Id);

            var gift = inventory.GetItem(itemId);
            if (gift.IsNull()) return 0;

            var giftedItemCount = amount;
            if (gift.Amount >= amount)
            {
                inventory.RemoveItem(gift.ItemId, amount, tag: gift.Tag);
            }
            else
            {
                giftedItemCount = (int)gift.Amount;
                inventory.RemoveStack(gift.Id);
            }

            var recvInventory = inventoryProvider.Get(receiver.Id);
            recvInventory.AddItem(itemId, giftedItemCount, tag: gift.Tag);
            recvInventory.EquipBestItems();

            gameData.Add(gameData.CreateSessionEvent(GameEventType.ItemAdd, gameData.GetSession(token.SessionId), new ItemAdd
            {
                UserId = receiverUserId,
                Amount = giftedItemCount,
                ItemId = itemId
            }));

            return giftedItemCount;
        }

        public bool EquipItem(SessionToken token, string userId, Guid itemId)
        {
            var character = GetCharacter(token, userId);
            if (character == null) return false;

            var item = gameData.GetItem(itemId);
            if (item == null) return false;


            var inventory = inventoryProvider.Get(character.Id);
            var invItem = inventory.GetItem(itemId);

            var skills = gameData.GetSkills(character.SkillsId);
            if (invItem.IsNull() || !PlayerInventory.CanEquipItem(gameData.GetItem(invItem.ItemId), skills))
                return false;

            return inventory.EquipItem(itemId);
        }

        public bool UnEquipItem(SessionToken token, string userId, Guid itemId)
        {
            var character = GetCharacter(token, userId);
            if (character == null) return false;

            var inventory = inventoryProvider.Get(character.Id);
            var invItem = inventory.GetEquippedItem(itemId);
            if (invItem.IsNull()) return false;

            return inventory.UnequipItem(itemId);
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

            var inventory = inventoryProvider.Get(character.Id);
            var items = inventory.GetEquippedItems();
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

            var inventory = inventoryProvider.Get(character.Id);
            var items = inventory.GetAllItems();
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

        public IReadOnlyList<PlayerFull> GetFullPlayers()
        {
            var chars = gameData.GetCharacters();
            return chars.Select(x => new
            {
                User = gameData.GetUser(x.UserId),
                Character = x
            })
            .Where(x => x.Character != null && x.User != null)
            .Select(x => x.User.MapFull(gameData, x.Character))
            .ToList();
        }

        public IReadOnlyList<Player> GetPlayers()
        {
            var chars = gameData.GetCharacters();
            return chars.Select(x => new
            {
                User = gameData.GetUser(x.UserId),
                Character = x
            })
            .Where(x => x.Character != null && x.User != null)
            .Select(x => x.User.Map(gameData, x.Character))
            .ToList();
        }

        public bool UpdateStatistics(SessionToken token, string userId, decimal[] statistics, Guid? characterId = null)
        {
            var character = characterId != null
                ? gameData.GetCharacter(characterId.Value)
                : GetCharacter(token, userId);

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

        public bool UpdateAppearance(string userId, string identifier, Models.SyntyAppearance appearance)
        {
            try
            {
                var user = gameData.GetUser(userId);
                if (user == null) return false;

                var character = gameData.GetCharacterByUserId(user.Id, identifier);
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
            catch (Exception exc)
            {
                logger.LogError("Exception updating appearance: " + exc.ToString());
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

        private Player GetPlayerByUser(User user, string identifier)
        {
            var userCharacters = gameData.GetCharacters(x => x.UserId == user.Id);
            var hasIndex = int.TryParse(identifier, out var index);
            index = index > 0 ? index - 1 : 0;

            foreach (var c in userCharacters)
            {
                if (identifier == null)
                {
                    return c.Map(gameData, user);
                }

                if (c.Identifier != null && c.Identifier.Equals(identifier, StringComparison.OrdinalIgnoreCase))
                {
                    return c.Map(gameData, user);
                }

                if (hasIndex && index == c.CharacterIndex)
                {
                    return c.Map(gameData, user);
                }
            }
            //var character = gameData.FindCharacter(x => x.UserId == user.Id);
            return null;
        }

        private PlayerExtended GetPlayerExtended(User user, string identifier)
        {
            var character = gameData.GetCharacterByUserId(user.Id, identifier);
            if (character == null) return new PlayerExtended
            {
                Appearance = new Models.SyntyAppearance(),
                Clan = new Models.Clan(),
                InventoryItems = new List<Models.InventoryItem>(),
                Skills = new SkillsExtended(),
                Resources = new Models.Resources(),
                State = new Models.CharacterState(),
                Statistics = new Models.Statistics()
            };
            return character.MapExtended(gameData, user);
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
            int[] level,
            decimal[] experience,
            Guid? characterId = null)
        {
            try
            {
                var gameSession = gameData.GetSession(token.SessionId);
                var character = (characterId != null ? gameData.GetCharacter(characterId.Value) : GetCharacter(token, userId)) ?? GetCharacter(token, userId);
                if (character == null)
                    throw new Exception("Unable to save exp. Character for user ID " + userId + " could not be found.");

                var removeFromSession = !AcquiredUserLock(token, character) && character.UserIdLock != null;
                var sessionOwner = gameData.GetUser(gameSession.UserId);
                var expLimit = sessionOwner.IsAdmin.GetValueOrDefault() ? 5000 : 50;

                var skills = gameData.GetSkills(character.SkillsId);

                var sessionState = gameData.GetSessionState(gameSession.Id);


                if (experience == null)
                    return false; // no skills was updated. Ignore
                // throw new Exception($"Unable to save exp. Client didnt supply experience, or experience was null. Character with name {character.Name} game session: " + gameSession.Id + ".");

                var characterSessionState = gameData.GetCharacterSessionState(token.SessionId, character.Id);
                var gains = characterSessionState.ExpGain;
                var savedSkillsCount = 0;
                for (var skillIndex = 0; skillIndex < experience.Length; ++skillIndex)
                {
                    var sl = level != null ? level[skillIndex] : 0;
                    var xp = experience[skillIndex];

                    if (sl == 0)
                    {
                        var maxXP = GameMath.OLD_LevelToExperience(170);
                        var curLevel = skills.GetLevel(skillIndex);
                        var minXP = GameMath.OLD_LevelToExperience(curLevel);
                        if (xp > minXP && xp < maxXP && curLevel <= 170)
                        {
                            sl = GameMath.OLD_ExperienceToLevel(xp);
                            xp -= GameMath.OLD_LevelToExperience(sl);
                            logger.LogWarning(character.Name + ". (Skill Index: " + skillIndex + ") Client did not provide level data. Saving using old way of saving. Session: " + sessionOwner?.UserName + " (" + gameSession.Id + "), Client Version: " + sessionState.ClientVersion);
                        }
                    }

                    if (level == null && sl == 0)
                        continue;
                    // throw new Exception("Unable to save exp for " + character.Name + ". Client did not provide level data. Session: " + sessionOwner?.UserName + " (" + gameSession.Id + "), Client Version: " + sessionState.ClientVersion);

                    ++savedSkillsCount;
                    if (sl <= 170 &&
                        experience[skillIndex] >= GameMath.ExperienceForLevel(sl))
                    {
                        xp -= GameMath.OLD_LevelToExperience(sl);
                        if (xp < 0) xp = 0;
                    }


                    skills.SetLevel(skillIndex, sl, experience[skillIndex]);
                }

                if (savedSkillsCount != experience.Length)
                {
                    logger.LogError(character.Name + " could only save " + savedSkillsCount + " out of " + experience.Length + " skills. Client did not provide level data. Saving using old way of saving. Session: " + sessionOwner?.UserName + " (" + gameSession.Id + "), Client Version: " + sessionState.ClientVersion);
                }

                if (removeFromSession)
                {
                    var activeSessionOwner = gameData.GetUser(character.UserIdLock.GetValueOrDefault());
                    if (activeSessionOwner != null)
                    {
                        SendRemovePlayerFromSession(character, gameSession);
                        logger.LogError($"{character.Name} was saved from a session it was not apart of. Session owner: {sessionOwner.UserName}, but character is part of {activeSessionOwner.UserName}.");
                    }
                }

                return true;
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                return false;
            }
        }

        //private void SetSkillLevel(int skillIndex, Skills skills, int v1, decimal v2)
        //{
        //    skills.AttackLevel = skills.AttackLevel > level[skillIndex] ? skills.AttackLevel : level[skillIndex];
        //    skills.Attack += GetDelta(expLimit, gains.Attack, skills.Attack, experience[skillIndex++]);
        //    skills.Defense += GetDelta(expLimit, gains.Defense, skills.Defense, experience[skillIndex++]);
        //    skills.Strength += GetDelta(expLimit, gains.Strength, skills.Strength, experience[skillIndex++]);
        //    skills.Health += GetDelta(expLimit, gains.Health, skills.Health, experience[skillIndex++]);
        //    skills.Woodcutting += GetDelta(expLimit, gains.Woodcutting, skills.Woodcutting, experience[skillIndex++]);
        //    skills.Fishing += GetDelta(expLimit, gains.Fishing, skills.Fishing, experience[skillIndex++]);
        //    skills.Mining += GetDelta(expLimit, gains.Mining, skills.Mining, experience[skillIndex++]);
        //    skills.Crafting += GetDelta(expLimit, gains.Crafting, skills.Crafting, experience[skillIndex++]);
        //    skills.Cooking += GetDelta(expLimit, gains.Cooking, skills.Cooking, experience[skillIndex++]);
        //    skills.Farming += GetDelta(expLimit, gains.Farming, skills.Farming, experience[skillIndex++]);
        //    skills.Slayer += GetDelta(expLimit, gains.Slayer, skills.Slayer, experience[skillIndex++]);
        //    skills.Magic += GetDelta(expLimit, gains.Magic, skills.Magic, experience[skillIndex++]);
        //    skills.Ranged += GetDelta(expLimit, gains.Ranged, skills.Ranged, experience[skillIndex++]);
        //    skills.Sailing += GetDelta(expLimit, gains.Sailing, skills.Sailing, experience[skillIndex++]);
        //}

        public void EquipBestItems(Character character)
        {
            inventoryProvider.Get(character.Id).EquipBestItems();
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
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                return false;
            }
        }

        private void SendRemovePlayerFromSession(Character character, DataModels.GameSession gameSession)
        {
            var characterUser = gameData.GetUser(character.UserId);
            var gameEvent = gameData.CreateSessionEvent(
                GameEventType.PlayerRemove,
                gameSession,
                new PlayerRemove()
                {
                    Reason = $"{character.Name} joined another session.",
                    UserId = characterUser.UserId
                });

            gameData.Add(gameEvent);
        }

        private Player CreatePlayer(
            DataModels.GameSession session, string userId, string userName, string identifier)
        {
            var user = gameData.GetUser(userId);
            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    UserName = userName,
                    Created = DateTime.UtcNow
                };
                gameData.Add(user);
            }

            return CreatePlayer(session, user, identifier);
        }

        private Player CreatePlayer(DataModels.GameSession session, User user, string identifier)
        {
            var userCharacters = gameData.GetCharacters(x => x.UserId == user.Id);
            var index = 0;
            if (userCharacters.Count > 0)
            {
                index = userCharacters.Max(x => x.CharacterIndex) + 1;
            }

            if (userCharacters.Count >= MaxCharacterCount)
            {
                return null;
            }

            var character = new Character
            {
                Id = Guid.NewGuid(),
                Name = user.UserName,
                UserId = user.Id,
                OriginUserId = session?.UserId ?? Guid.Empty,
                Created = DateTime.UtcNow,
                Identifier = identifier,
                CharacterIndex = index
            };

            var appearance = GenerateRandomAppearance();
            var syntyAppearance = GenerateRandomSyntyAppearance();

            var skills = GenerateSkills();
            var resources = GenerateResources();
            var statistics = GenerateStatistics();
            var state = new DataModels.CharacterState()
            {
                Id = Guid.NewGuid(),
                Health = 10,
            };

            gameData.Add(state);
            gameData.Add(syntyAppearance);
            gameData.Add(statistics);
            gameData.Add(skills);
            gameData.Add(appearance);
            gameData.Add(resources);

            character.StateId = state.Id;
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
                HealthLevel = 10,
                AttackLevel = 1,
                CraftingLevel = 1,
                CookingLevel = 1,
                DefenseLevel = 1,
                FarmingLevel = 1,
                FishingLevel = 1,
                MagicLevel = 1,
                MiningLevel = 1,
                RangedLevel = 1,
                SailingLevel = 1,
                SlayerLevel = 1,
                StrengthLevel = 1,
                WoodcuttingLevel = 1
            };
        }

        private Character GetCharacter(SessionToken token, string userId)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null) return null;

            var user = gameData.GetUser(userId);
            if (user == null) return null;

            var sessionCharacters = gameData.GetSessionCharacters(session);
            var character = sessionCharacters.FirstOrDefault(x => x.UserId == user.Id);
            //var chars = sessionCharacters.Where(x => x.UserId == user.Id).ToList();
            // if (character == null)
            // {
            //     return gameData.GetCharacterByUserId(user.Id, "1");
            // }

            return character;
        }

        private DataModels.CharacterState CreateCharacterState(CharacterStateUpdate update)
        {
            var state = new DataModels.CharacterState
            {
                Id = Guid.NewGuid(),
                DuelOpponent = update.DuelOpponent,
                Health = update.Health,
                InArena = update.InArena,
                InRaid = update.InRaid,
                Island = update.Island,
                Task = update.Task,
                TaskArgument = update.TaskArgument
            };
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
