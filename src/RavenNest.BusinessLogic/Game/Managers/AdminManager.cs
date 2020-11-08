using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Net;
using RavenNest.DataModels;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using InventoryItem = RavenNest.DataModels.InventoryItem;

namespace RavenNest.BusinessLogic.Game
{

    public class AdminManager : IAdminManager
    {
        private readonly ILogger<AdminManager> logger;
        private readonly IPlayerInventoryProvider inventoryProvider;
        private readonly IItemResolver itemResolver;
        private readonly IPlayerManager playerManager;
        private readonly IGameData gameData;
        private readonly ISessionManager sessionManager;

        public AdminManager(
            ILogger<AdminManager> logger,
            IPlayerInventoryProvider inventoryProvider,
            IItemResolver itemResolver,
            IPlayerManager playerManager,
            IGameData gameData,
            ISessionManager sessionManager,
            ISecureHasher secureHasher)
        {
            this.logger = logger;
            this.inventoryProvider = inventoryProvider;
            this.itemResolver = itemResolver;
            this.playerManager = playerManager;
            this.gameData = gameData;
            this.sessionManager = sessionManager;
        }

        public bool NerfItems()
        {
            return true;
        }

        //public bool FixCharacterExpGain(Guid characterId)
        //{
        //    var character = gameData.GetCharacter(characterId);
        //    if (character == null) return false;
        //    var playerSkills = gameData.GetSkills(character.SkillsId);
        //    if (playerSkills == null) return false;

        //    var skills = playerSkills.GetSkills();
        //    foreach (var skill in skills)
        //    {
        //        var level = skill.Level;
        //        var cappedLevel = level > 170 ? 170 : level;
        //        var expBase = GameMath.OLD_ExperienceToLevel(cappedLevel);

        //        var newLevelDiff = level - cappedLevel;
        //        var totalGainedExp = 0m;
        //        for (var i = 1; i <= newLevelDiff; ++i)
        //        {
        //            totalGainedExp += GameMath.ExperienceForLevel(level + i);
        //        }



        //        skill.Experience
        //    }
        //}

        public bool ProcessItemRecovery(string query, string identifier)
        {
            try
            {
                var items = itemResolver.Resolve(query, identifier);

                foreach (var charItems in items.GroupBy(x => x.Character.Id))
                {
                    var inventory = inventoryProvider.Get(charItems.Key);
                    var invItems = inventory.GetInventoryItems();
                    foreach (var item in charItems)
                    {
                        inventory.AddItem(item.Item.Id, (long)item.Amount);
                    }
                }

                return true;
            }
            catch (Exception exc)
            {
                logger.LogError("Failed to recover items: " + exc);
                return false;
            }
        }

        public bool KickPlayer(string userId, string identifier)
        {
            var character = gameData.GetCharacterByUserId(userId, identifier);
            var userToRemove = gameData.GetUser(character.UserId);
            if (userToRemove == null)
                return false;

            var currentSession = gameData.GetSessionByUserId(userId);
            //var currentSession = gameData.GetUserSession(character.UserIdLock.GetValueOrDefault());
            if (currentSession == null)
                return false;

            var characterUser = gameData.GetUser(character.UserId);
            var gameEvent = gameData.CreateSessionEvent(
                GameEventType.PlayerRemove,
                currentSession,
                new PlayerRemove()
                {
                    Reason = $"{character.Name} was kicked remotely.",
                    UserId = characterUser.UserId,
                    CharacterId = character.Id
                });

            gameData.Add(gameEvent);
            return true;
        }

        public bool SuspendPlayer(string userId, string identifier)
        {
            // 1. kick player
            // 2. block player from joining any games.
            return false;
        }

        public bool ResetUserPassword(string userId)
        {
            var user = gameData.GetUser(userId);
            if (user == null)
                return false;

            user.PasswordHash = null;
            return true;
        }

        public bool FixCharacterIndices(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                var users = gameData.GetUsers();
                foreach (var u in users)
                {
                    FixCharacterIndices(u);
                }
                return true;
            }

            var user = gameData.GetUser(userId);
            if (user == null)
                return false;

            return FixCharacterIndices(user);
        }

        private bool FixCharacterIndices(User user)
        {
            var characters = gameData
                .GetCharacters(x => x.UserId == user.Id)
                .OrderBy(x => x.Created)
                .ToList();

            var index = 0;
            foreach (var c in characters)
            {
                c.CharacterIndex = index++;
            }

            return true;
        }

        public bool SetCraftingRequirements(string itemQuery, string requirementQuery)
        {
            var targetItem = itemResolver.Resolve(itemQuery).FirstOrDefault();
            if (targetItem == null)
                return false;

            var requirements = itemResolver.Resolve(requirementQuery);
            if (requirements.Count == 0)
                return false;

            var existingRequirements = gameData.GetCraftingRequirements(targetItem.Item.Id);
            foreach (var er in existingRequirements)
            {
                gameData.Remove(er);
            }

            foreach (var req in requirements)
            {
                gameData.Add(new DataModels.ItemCraftingRequirement
                {
                    Id = Guid.NewGuid(),
                    Amount = (int)req.Amount,
                    ItemId = targetItem.Item.Id,
                    ResourceItemId = req.Item.Id
                });
            }
            return true;
        }

        public bool MergePlayerAccounts(string userId)
        {
            var user = gameData.GetUser(userId);
            if (user == null)
                return false;

            var characters = gameData
                .GetCharacters(x => x.Name.Equals(user.UserName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Revision)
                .ToList();

            var main = gameData.GetCharacterByUserId(user.Id, "1");
            if (main == null)
                return false;

            var mainSkills = gameData.GetSkills(main.SkillsId);
            var mainResources = gameData.GetResources(main.ResourcesId);

            var mainInventory = gameData.GetInventoryItems(main.Id);
            var mainStatistics = gameData.GetStatistics(main.StatisticsId);

            foreach (var alt in characters)
            {
                if (alt.Id == main.Id || alt.UserId == main.UserId)
                    continue;

                var altSkills = gameData.GetSkills(alt.SkillsId);
                if (altSkills != null)
                {
                    MergeSkills(mainSkills, altSkills);
                    gameData.Remove(altSkills);
                }

                var altResources = gameData.GetResources(alt.ResourcesId);
                if (altResources != null)
                {
                    MergeResources(mainResources, altResources);
                    gameData.Remove(altResources);
                }

                var altItems = gameData.GetAllPlayerItems(alt.Id);
                foreach (var altItem in altItems)
                {

                    var mainItem = mainInventory.FirstOrDefault(x => x.ItemId == altItem.ItemId);
                    if (mainItem != null)
                    {
                        mainItem.Amount += altItem.Amount;
                    }
                    else
                    {
                        if (altItem.Amount > 0)
                        {
                            gameData.Add(new DataModels.InventoryItem
                            {
                                Id = Guid.NewGuid(),
                                Amount = altItem.Amount,
                                CharacterId = main.Id,
                                Equipped = false,
                                ItemId = altItem.ItemId
                            });
                        }
                    }
                    gameData.Remove(altItem);
                }

                var altStatistics = gameData.GetStatistics(alt.StatisticsId);
                if (altStatistics != null)
                {
                    MergeStatistics(mainStatistics, altStatistics);
                    gameData.Remove(altStatistics);
                }

                gameData.Remove(alt);

                var altUser = gameData.GetUser(alt.UserId);
                if (altUser != null)
                {
                    gameData.Remove(altUser);
                }
            }

            return FixCharacterIndices(user);
        }

        public PagedPlayerCollection GetPlayersPaged(int offset, int size, string sortOrder, string query)
        {
            var allPlayers = playerManager.GetFullPlayers();

            allPlayers = FilterByQuery(query, allPlayers);
            allPlayers = OrderBy(sortOrder, allPlayers);

            return new PagedPlayerCollection()
            {
                TotalSize = allPlayers.Count,
                Items = allPlayers.Skip(offset).Take(size).ToList()
            };
        }

        public PagedSessionCollection GetSessionsPaged(int offset, int size, string sortOrder, string query)
        {
            try
            {
                var activeSessions = gameData.GetActiveSessions();
                var lastActiveRange = DateTime.UtcNow.AddMinutes(-30);
                activeSessions = FilterByQuery(query, activeSessions.Where(x => x.Updated >= lastActiveRange));
                activeSessions = OrderBy(sortOrder, activeSessions);

                return new PagedSessionCollection()
                {
                    TotalSize = activeSessions.Count,
                    Items = activeSessions.Skip(offset).Take(size)
                        .Select(x => ModelMapper.Map(gameData, x))
                        .Where(x => x != null)
                        .ToList()
                };
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                return null;
            }
        }

        public bool UpdatePlayerSkill(string userId, string skill, decimal experience, string identifier)
        {
            var character = this.gameData.GetCharacterByUserId(userId, identifier);
            if (character == null) return false;

            var skills = this.gameData.GetSkills(character.SkillsId);
            if (skills == null) return false;

            var playerSession = gameData.GetSessionByUserId(userId);
            if (playerSession == null) return true;

            SetValue(skills, skill, experience);

            var gameEvent = gameData.CreateSessionEvent(
                GameEventType.PlayerExpUpdate,
                playerSession,
                new PlayerExpUpdate
                {
                    UserId = userId,
                    Skill = skill,
                    Experience = experience
                });

            gameData.Add(gameEvent);
            return true;
        }

        public bool UpdatePlayerName(string userid, string name, string identifier)
        {
            var character = this.gameData.GetCharacterByUserId(userid, identifier);
            if (character == null) return false;
            character.Name = name;

            var playerSession = gameData.GetSessionByUserId(userid);
            if (playerSession == null) return true;

            var gameEvent = gameData.CreateSessionEvent(
                GameEventType.PlayerNameUpdate,
                playerSession,
                new PlayerNameUpdate
                {
                    UserId = userid,
                    Name = name
                });

            gameData.Add(gameEvent);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetValue<T, T2>(T obj, string property, T2 value)
        {
            var prop = typeof(T).GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            prop.SetValue(obj, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IReadOnlyList<T> OrderBy<T>(string sortOrder, IEnumerable<T> allPlayers)
        {
            if (!string.IsNullOrEmpty(sortOrder))
            {
                var sortProperty = typeof(T)
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(x => x.Name == sortOrder.Substring(1));

                var ascending = sortOrder[0] == '+' || sortOrder[0] == '1';
                if (sortProperty != null)
                {
                    allPlayers = (ascending
                        ? allPlayers.OrderBy(x => sortProperty.GetValue(x))
                        : allPlayers.OrderByDescending(x => sortProperty.GetValue(x)));
                }
            }

            return allPlayers.ToList();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IReadOnlyList<T> FilterByQuery<T>(string query, IEnumerable<T> allPlayers)
        {
            if (!string.IsNullOrEmpty(query) && query != "0" && query != "-")
            {
                var stringProps = typeof(T)
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => x.PropertyType == typeof(string));

                allPlayers = allPlayers.Where(x => stringProps.Any(y =>
                {
                    var value = y.GetValue(x)?.ToString();
                    return !string.IsNullOrEmpty(value) && value.Contains(query);
                }));
            }

            return allPlayers.ToList();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MergeStatistics(DataModels.Statistics main, DataModels.Statistics alt)
        {
            // not important right now
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MergeResources(DataModels.Resources main, DataModels.Resources alt)
        {
            main.Arrows += alt.Arrows;
            main.Coins += alt.Coins;
            main.Fish += alt.Fish;
            main.Magic += alt.Magic;
            main.Ore += alt.Ore;
            main.Wheat += alt.Wheat;
            main.Wood += alt.Wood;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MergeSkills(DataModels.Skills mainSkills, DataModels.Skills altSkills)
        {
            mainSkills.Attack += altSkills.Attack;
            mainSkills.Cooking += altSkills.Cooking;
            mainSkills.Crafting += altSkills.Crafting;
            mainSkills.Defense += altSkills.Defense;
            mainSkills.Farming += altSkills.Farming;
            mainSkills.Fishing += altSkills.Fishing;
            mainSkills.Health += altSkills.Health;
            mainSkills.Magic += altSkills.Magic;
            mainSkills.Mining += altSkills.Mining;
            mainSkills.Ranged += altSkills.Ranged;
            mainSkills.Sailing += altSkills.Sailing;
            mainSkills.Slayer += altSkills.Slayer;
            mainSkills.Strength += altSkills.Strength;
            mainSkills.Woodcutting += altSkills.Woodcutting;
        }

        public bool RefreshVillageInfo()
        {
            var sessions = gameData.GetActiveSessions();
            foreach (var session in sessions)
            {
                sessionManager.SendVillageInfo(session);
            }

            return true;
        }

        public async Task<bool> RefreshPermissionsAsync()
        {
            var sessions = gameData.GetActiveSessions();
            foreach (var session in sessions)
            {
                await sessionManager.SendPermissionDataAsync(session);
            }
            return true;
        }
    }
}
