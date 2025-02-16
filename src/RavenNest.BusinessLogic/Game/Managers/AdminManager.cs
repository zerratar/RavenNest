using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Providers;
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

    public class AdminManager
    {
        private readonly ILogger<AdminManager> logger;
        private readonly Random random;
        private readonly PlayerInventoryProvider inventoryProvider;
        private readonly IEntityResolver itemResolver;
        private readonly PlayerManager playerManager;
        private readonly GameData gameData;
        private readonly SessionManager sessionManager;
        private readonly ISecureHasher secureHasher;

        public AdminManager(
            ILogger<AdminManager> logger,
            PlayerInventoryProvider inventoryProvider,
            IEntityResolver itemResolver,
            PlayerManager playerManager,
            GameData gameData,
            SessionManager sessionManager,
            ISecureHasher secureHasher)
        {
            this.logger = logger;
            this.random = new Random();
            this.inventoryProvider = inventoryProvider;
            this.itemResolver = itemResolver;
            this.playerManager = playerManager;
            this.gameData = gameData;
            this.sessionManager = sessionManager;
            this.secureHasher = secureHasher;
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

        public GameSessionPlayerCache GetRandomStateCache(int playerCount, TimeSpan? inactiveForAtLeast = null)
        {
            var players = new List<GameSessionPlayerCache.GameCachePlayerItem>();
            var uid = new HashSet<Guid>();
            var inactivityDate = inactiveForAtLeast != null ? DateTime.UtcNow.Subtract(inactiveForAtLeast.Value) : DateTime.MaxValue;
            foreach (var user in gameData.GetUsers().OrderBy(x => random.Next()))
            {
                if (uid.Add(user.Id))
                {
                    var character = gameData.GetCharacterByUserId(user.Id);
                    if (character == null)
                    {
                        uid.Remove(user.Id);
                        continue;
                    }

                    if (inactiveForAtLeast != null && character.LastUsed > inactivityDate)
                    {
                        uid.Remove(user.Id);
                        continue;
                    }

                    var userAccess = gameData.GetUserAccess(character.UserId).FirstOrDefault();
                    players.Add(new GameSessionPlayerCache.GameCachePlayerItem
                    {
                        CharacterId = character.Id,
                        CharacterIndex = character.CharacterIndex,
                        User = new GameSessionPlayerCache.GameCachePlayerItem.SocialMediaUser(
                                user.Id, character.Id, user.UserName, user.DisplayName, "",
                                userAccess.Platform, userAccess.PlatformId, false, false, false, false, character.Identifier)
                    });
                }

                if (uid.Count >= playerCount)
                    break;
            }

            return new GameSessionPlayerCache()
            {
                Created = DateTime.UtcNow,
                Players = players
            };
        }

        public GameSessionPlayerCache GetStreamerStateCache(string streamer)
        {
            // 1., resolve the streamer, is it username, twitch user id or user id guid
            var streamerUser = GetUser(streamer);
            if (streamerUser == null)
            {
                return new GameSessionPlayerCache();
            }
            return GetStreamerStateCache(streamerUser);
        }

        public GameSessionPlayerCache GetStreamerStateCache(Guid userId)
        {
            // 1., resolve the streamer, is it username, twitch user id or user id guid
            var streamerUser = gameData.GetUser(userId);
            if (streamerUser == null)
            {
                return new GameSessionPlayerCache();
            }

            return GetStreamerStateCache(streamerUser);
        }

        public GameSessionPlayerCache GetStreamerStateCache(User streamerUser)
        {
            var players = new List<GameSessionPlayerCache.GameCachePlayerItem>();
            var characters = gameData.GetCharactersByUserLock(streamerUser.Id).AsList();

            try
            {
                var folder = System.IO.Path.Combine(FolderPaths.GeneratedDataPath, FolderPaths.SessionPlayers);
                var playerlistFile = System.IO.Path.Combine(folder, streamerUser.Id.ToString() + ".json");
                if (System.IO.File.Exists(playerlistFile))
                {
                    var existing = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Guid>>(System.IO.File.ReadAllText(playerlistFile));
                    foreach (var e in existing)
                    {
                        var c = gameData.GetCharacter(e);
                        if (c == null || (c.UserIdLock != null && c.UserIdLock != streamerUser.Id))
                            continue;

                        if (!characters.Any(x => x.Id == c.Id))
                        {
                            characters.Add(c);
                        }
                    }
                }
            }
            catch { }

            foreach (var character in characters)
            {
                var user = gameData.GetUser(character.UserId);
                var userAccess = gameData.GetUserAccess(character.UserId).FirstOrDefault();
                players.Add(new GameSessionPlayerCache.GameCachePlayerItem
                {
                    CharacterId = character.Id,
                    CharacterIndex = character.CharacterIndex,
                    User = new GameSessionPlayerCache.GameCachePlayerItem.SocialMediaUser(
                            user.Id, character.Id, user.UserName, user.DisplayName, "",
                            userAccess.Platform, userAccess.PlatformId, false, false, false, false, character.Identifier)
                });
            }

            return new GameSessionPlayerCache()
            {
                Created = DateTime.UtcNow,
                Players = players
            };
        }

        private User GetUser(string streamer)
        {
            if (Guid.TryParse(streamer, out var userId))
            {
                var user = gameData.GetUser(userId);
                if (user != null)
                {
                    return user;
                }
            }

            return gameData.GetUsers().FirstOrDefault(x => x.UserId == streamer || x.UserName != null && x.UserName.Equals(streamer, StringComparison.OrdinalIgnoreCase));
        }

        public bool AddCoins(string query, string identifier)
        {
            var character = itemResolver.ResolveCharacter(query, identifier);
            if (character == null) return false;
            var amount = query.Split(' ').LastOrDefault();
            if (long.TryParse(amount, out var amountValue))
            {
                var resx = gameData.GetResources(character);
                resx.Coins += amountValue;
                return true;
            }

            return false;
        }

        public bool ProcessItemRecovery(string query, string identifier)
        {
            try
            {
                var items = itemResolver.ResolvePlayerAndItems(query, identifier);

                foreach (var charItems in items.GroupBy(x => x.Character.Id))
                {
                    var inventory = inventoryProvider.Get(charItems.Key);
                    var invItems = inventory.GetUnequippedItems();
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


        public bool FixLoyaltyPoints()
        {
            try
            {
                var allUsers = gameData.GetUsers();
                foreach (var user in allUsers)
                {
                    var loyalties = gameData.GetUserLoyalties(user.Id);
                    foreach (var loyalty in loyalties)
                    {
                        //for (var i = 1; i < loyalty.Level; ++i)
                        //{
                        //    var addedPoints = UserLoyalty.GetLoyaltyPoints(i) - 100;
                        //    loyalty.Points += addedPoints;
                        //}

                        if (loyalty.Points < 0)
                        {
                            loyalty.Points += 100;
                        }
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool KickPlayer(Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            var userToRemove = gameData.GetUser(character.UserId);
            if (userToRemove == null)
                return false;

            //var currentSession = gameData.GetSessionByUserId(userId);
            var currentSession = gameData.GetSessionByUserId(character.UserIdLock.GetValueOrDefault());
            if (currentSession == null)
                return false;

            var characterUser = gameData.GetUser(character.UserId);

            if (character.UserIdLock != null)
                character.PrevUserIdLock = character.UserIdLock;

            character.UserIdLock = null;

            var gameEvent = gameData.CreateSessionEvent(GameEventType.PlayerRemove,
                currentSession,
                new PlayerRemove()
                {
                    Reason = $"{character.Name} was kicked remotely.",
                    UserId = characterUser.Id,
                    CharacterId = character.Id
                });

            gameData.EnqueueGameEvent(gameEvent);

            return true;
        }

        public bool SuspendPlayer(string userId)
        {
            // 1. kick player
            // 2. block player from joining any games.
            return false;
        }

        public bool ResetUserPassword(string userId)
        {
            var user = gameData.GetUserByTwitchId(userId);
            if (user == null)
                return false;

            user.PasswordHash = null;
            return true;
        }

        public bool SetSkillLevel(string twitchUserId, string identifier, string skill, int level, double experience = 0)
        {
            var user = gameData.FindUser(twitchUserId);
            if (user == null)
            {
                return false;
            }

            if (level < 1 || level > GameMath.MaxLevel)
            {
                return false;
            }

            var character = gameData.GetCharacterByUserId(user.Id, identifier);
            if (character == null)
            {
                return false;
            }

            var skills = gameData.GetCharacterSkills(character.SkillsId);
            if (skills == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(skill))
            {
                return false;
            }

            var allSkills = skills.GetSkills();
            var targetSkill = allSkills.FirstOrDefault(x => x.Name.Equals(skill.Trim(), StringComparison.CurrentCultureIgnoreCase));
            if (targetSkill == null)
            {
                return false;
            }

            targetSkill.Level = level;
            targetSkill.Experience = experience;
            return true;
        }

        public bool FixCharacterIndices()
        {
            foreach (var user in gameData.GetUsers())
            {
                var characters = gameData.GetCharactersByUserId(user.Id).OrderBy(x => x.CharacterIndex).ToArray();
                var index = 0;
                var prevAlias = "";
                foreach (var c in characters)
                {
                    c.CharacterIndex = index++;
                    if (c.Identifier == prevAlias && !string.IsNullOrWhiteSpace(c.Identifier))
                    {
                        c.Identifier = c.CharacterIndex.ToString();
                    }
                }
            }

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

            var user = gameData.GetUserByTwitchId(userId);
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
            var targetItem = itemResolver.ResolveItems(itemQuery).FirstOrDefault();
            if (targetItem == null)
                return false;

            var requirements = itemResolver.ResolveItems(requirementQuery);
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

        public bool MergePlayerAccounts(Guid userId)
        {
            var user = gameData.GetUser(userId);
            if (user == null)
                return false;

            var characters = gameData
                .GetCharacters(x => x.Name.Equals(user.UserName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var main = gameData.GetCharacterByUserId(user.Id, "1");
            if (main == null)
                return false;

            var mainSkills = gameData.GetCharacterSkills(main.SkillsId);
            var mainInventory = gameData.GetInventoryItems(main.Id);
            var mainStatistics = gameData.GetStatistics(main.StatisticsId);

            foreach (var alt in characters)
            {
                if (alt.Id == main.Id || alt.UserId == main.UserId)
                    continue;

                var altSkills = gameData.GetCharacterSkills(alt.SkillsId);
                if (altSkills != null)
                {
                    MergeSkills(mainSkills, altSkills);
                    gameData.Remove(altSkills);
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
                                ItemId = altItem.ItemId,
                                Soulbound = false,
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
            var allPlayers = playerManager.GetWebsiteAdminPlayers();

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

        public bool UpdatePlayerSkill(Guid characterId, string skill, double experience)
        {
            var character = this.gameData.GetCharacter(characterId);
            if (character == null) return false;

            var skills = this.gameData.GetCharacterSkills(character.SkillsId);
            if (skills == null) return false;

            //var playerSession = gameData.GetSessionByUserId(userId);
            var playerSession = gameData.GetSessionByUserId(character.UserIdLock.GetValueOrDefault());
            if (playerSession == null) return true;

            var user = this.gameData.GetUser(character.UserId);

            SetValue(skills, skill, experience);

            var gameEvent = gameData.CreateSessionEvent(GameEventType.PlayerExpUpdate,
                playerSession,
                new PlayerExpUpdate
                {
                    PlayerId = characterId,
                    Skill = skill,
                    Experience = experience
                });

            gameData.EnqueueGameEvent(gameEvent);
            return true;
        }

        public bool UpdatePlayerName(Guid characterId, string name)
        {
            var character = this.gameData.GetCharacter(characterId);
            if (character == null) return false;
            character.Name = name;


            var playerSession = gameData.GetSessionByUserId(character.UserIdLock.GetValueOrDefault());
            //var playerSession = gameData.GetSessionByUserId(userid);
            if (playerSession == null) return true;

            var user = this.gameData.GetUser(character.UserId);

            var gameEvent = gameData.CreateSessionEvent(GameEventType.PlayerNameUpdate,
                playerSession,
                new PlayerNameUpdate
                {
                    PlayerId = characterId,
                    Name = name
                });

            gameData.EnqueueGameEvent(gameEvent);
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
                    .FirstOrDefault(x => x.Name == sortOrder[1..]);

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
            mainSkills.Gathering += altSkills.Gathering;
            mainSkills.Alchemy += altSkills.Alchemy;
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

        public bool RefreshPermissions()
        {
            var sessions = gameData.GetActiveSessions();
            foreach (var session in sessions)
            {
                if (session != null)
                {
                    sessionManager.SendSessionSettings(session);
                }
            }
            return true;
        }

        public bool SetPassword(string username, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword)) return false;
            var user = gameData.GetUserByUsername(username);
            if (user == null) return false;
            user.PasswordHash = secureHasher.Get(newPassword.Trim());
            return true;
        }
    }
}
