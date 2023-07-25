using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Game.Enchantment;
using RavenNest.BusinessLogic.Models;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Providers;
using RavenNest.BusinessLogic.Twitch.Extension;
using RavenNest.DataModels;
using RavenNest.Models;
using RavenNest.Models.TcpApi;
using Gender = RavenNest.DataModels.Gender;
using Item = RavenNest.DataModels.Item;
using Resources = RavenNest.DataModels.Resources;
using Skills = RavenNest.DataModels.Skills;
using Statistics = RavenNest.DataModels.Statistics;

namespace RavenNest.BusinessLogic.Game
{
    public class PlayerManager
    {
        public const int MaxCharacterCount = 3;
        private readonly ILogger logger;
        private readonly IRavenBotApiClient ravenbotApi;
        private readonly IPlayerHighscoreProvider highscoreProvider;
        private readonly PlayerInventoryProvider inventoryProvider;
        private readonly EnchantmentManager enchantmentManager;
        private readonly GameData gameData;
        private readonly IIntegrityChecker integrityChecker;
        private readonly ITwitchExtensionConnectionProvider extensionWsConnectionProvider;

        public PlayerManager(
            ILogger<PlayerManager> logger,
            IRavenBotApiClient ravenbotApi,
            IPlayerHighscoreProvider highscoreProvider,
            PlayerInventoryProvider inventoryProvider,
            EnchantmentManager enchantmentManager,
            GameData gameData,
            IIntegrityChecker integrityChecker,
            ITwitchExtensionConnectionProvider extensionWsConnectionProvider)
        {
            this.logger = logger;
            this.ravenbotApi = ravenbotApi;
            this.highscoreProvider = highscoreProvider;
            this.inventoryProvider = inventoryProvider;
            this.enchantmentManager = enchantmentManager;
            this.gameData = gameData;
            this.integrityChecker = integrityChecker;
            this.extensionWsConnectionProvider = extensionWsConnectionProvider;
        }

        public int GetHighscore(SessionToken sessionToken, Guid characterId, string skillName)
        {
            if (skillName == "all")
                skillName = null;

            var c = gameData.GetCharacter(characterId);
            if (c == null) return int.MaxValue;

            var u = gameData.GetUser(c.UserId);
            if (u == null) return int.MaxValue;

#if DEBUG
            if (u.IsHiddenInHighscore.GetValueOrDefault())
            {
                return -2;
            }
#else
            if (u.IsModerator.GetValueOrDefault() || u.IsAdmin.GetValueOrDefault() || u.IsHiddenInHighscore.GetValueOrDefault())
            {
                return -2;
            }
#endif

            var player = GetPlayer(characterId);
            var item = highscoreProvider.GetSkillHighScore(player, GetHighscorePlayers(), skillName);
            if (item != null)
            {
                return item.Rank;
            }

            return -1;
        }

        public async Task<Player> CreatePlayerIfNotExists(string userId, string platform, string userName, string identifier)
        {
            var player = GetPlayer(userId, platform, identifier);
            if (player != null) return player;
            return await CreatePlayer(userId, platform, userName, identifier);
        }

        public Task<Player> CreatePlayer(string userId, string platform, string userName, string identifier)
        {
            return CreateUserAndPlayer(null, new PlayerJoinData
            {
                PlatformId = userId,
                Platform = platform,
                UserName = userName,
                Identifier = identifier,
            });
        }

        public void SendPlayerTravelToGame(
            DataModels.GameSession activeSession,
            Character character,
            string target)
        {
            gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.PlayerTravel, activeSession, new PlayerTravel
            {
                Island = target,
                PlayerId = character.Id
            }));
        }

        public void SendRaidJoinToGame(DataModels.GameSession gameSession, Character character)
        {
            gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.PlayerJoinRaid, gameSession, new PlayerId
            {
                Id = character.Id
            }));
        }

        public void SendDungeonJoinToGame(DataModels.GameSession gameSession, Character character)
        {
            gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.PlayerJoinDungeon, gameSession, new PlayerId
            {
                Id = character.Id
            }));
        }

        public void SendRaidStartToGame(DataModels.GameSession gameSession, Character character)
        {
            gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.PlayerStartRaid, gameSession, new PlayerId
            {
                Id = character.Id
            }));
        }

        public void SendDungeonStartToGame(DataModels.GameSession gameSession, Character character)
        {
            gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.PlayerStartDungeon, gameSession, new PlayerId
            {
                Id = character.Id
            }));
        }

        public void SendPlayerEnterOnsenToGame(DataModels.GameSession gameSession, Character character)
        {
            gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.PlayerBeginRest, gameSession, new PlayerId
            {
                Id = character.Id
            }));
        }

        public void SendPlayerExitOnsenToGame(DataModels.GameSession gameSession, Character character)
        {
            gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.PlayerEndRest, gameSession, new PlayerId
            {
                Id = character.Id
            }));
        }

        public void SendPlayerTaskToGame(
            DataModels.GameSession activeSession,
            Character character,
            string task,
            string taskArgument)
        {
            var uid = character.UserId;
            var user = gameData.GetUser(uid);
            if (user == null)
            {
                return;
            }

            if (task.ToLower() == "all")
            {
                task = "fighting";
                taskArgument = "all";
            }

            var state = gameData.GetCharacterState(character.StateId);
            if (state != null)
            {
                state.Task = task;
                state.TaskArgument = taskArgument;
            }

            gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.PlayerTask, activeSession, new PlayerTask
            {
                Task = task,
                TaskArgument = taskArgument,
                PlayerId = character.Id
            }));
        }

        [Obsolete]
        public Task<PlayerJoinResult> AddPlayer(
            SessionToken token,
            string userId,
            string userName,
            string platform,
            string identifier = null)
        {
            return AddPlayer(token, new PlayerJoinData
            {
                Identifier = identifier,
                PlatformId = userId,
                Platform = platform,
                UserName = userName,
            });
        }

        public async Task<PlayerJoinResult> AddPlayer(DataModels.GameSession session, PlayerJoinData playerData)
        {
            var result = new PlayerJoinResult();
            var internalErrorMessage = "";
            try
            {
                if (session == null || ((SessionStatus)session.Status != SessionStatus.Active))
                {
                    result.ErrorMessage = "Game Session has been terminated.";
                    return result;
                }

                // Add by Character Id
                var characterId = playerData.CharacterId;
                if (characterId != Guid.Empty || Guid.TryParse(playerData.PlatformId ?? "", out characterId))
                {
                    result = await AddPlayerByCharacterId(session, characterId, playerData.IsGameRestore);
                    return result;
                }

                var userName = playerData.UserName;
                var identifier = playerData.Identifier;

                if (string.IsNullOrEmpty(identifier))
                {
                    identifier = "0";
                }

                var platformId = playerData.PlatformId;
                if (string.IsNullOrEmpty(platformId))
                {
                    result.Success = false;
                    result.ErrorMessage = "Player does not contain a valid UserId.";
                    return result;
                }

                var platform = playerData.Platform;
                if (string.IsNullOrEmpty(platform))
                {
                    result.Success = false;
                    result.ErrorMessage = "Player is not from a valid Platform.";
                    return result;
                }

                var user = gameData.GetUser(platformId, platform);
                if (user == null)
                {
                    result.Player = await CreateUserAndPlayer(session, playerData);
                    result.Success = result.Player != null;
                    result.IsNewUser = true;
                    if (!result.Success)
                    {
                        internalErrorMessage = "Unable to create a user. " + JSON.Stringify(playerData);
                    }
                    return result;
                }

                if (!string.IsNullOrEmpty(userName))
                {
                    user.UserName = userName;
                    user.DisplayName = userName;
                }

                if (user.Status.GetValueOrDefault() == (int)AccountStatus.TemporarilySuspended)
                {
                    result.Success = false;
                    result.ErrorMessage = "You have been temporarily suspended from playing. Contact the staff for more information.";
                    return result;
                }

                if (user.Status.GetValueOrDefault() == (int)AccountStatus.PermanentlySuspended)
                {
                    result.Success = false;
                    result.ErrorMessage = "You have been permanently suspended from playing. Contact the staff for more information.";
                    return result;
                }

                var loyalty = gameData.GetUserLoyalty(user.Id, session.UserId);
                if (loyalty == null)
                {
                    CreateUserLoyalty(session, user, playerData);
                }
                else
                {
                    loyalty.IsModerator = playerData.Moderator;
                    loyalty.IsSubscriber = playerData.Subscriber;
                    loyalty.IsVip = playerData.Vip;
                }

                var character = gameData.GetCharacterByUserId(user.Id, identifier);
                if (character == null)
                {
                    var player = await CreatePlayer(session, user, playerData);
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

                if (gameData.GetCharacterSkills(character.SkillsId) == null)
                {
                    var skills = gameData.GenerateSkills();
                    character.SkillsId = skills.Id;
                    gameData.Add(skills);
                }

                if (string.IsNullOrEmpty(character.Name) || (!string.IsNullOrEmpty(userName) && character.Name != userName))
                {
                    character.Name = userName;
                }

                if (playerData.IsGameRestore && (character.UserIdLock != null && character.UserIdLock != session.UserId))
                {
                    var newOwner = gameData.GetUser(character.UserIdLock.Value);
                    result.ErrorMessage = "Player has left to join " + newOwner.UserName + "'s stream.";
                    result.Success = false;
                    return result;
                }

                result.Player = await AddPlayerToSession(session, user, character);
                result.Success = result.Player != null;


                return result;
            }
            catch (Exception exc)
            {
                logger.LogError($"Unable to add player ({playerData?.PlatformId}, {playerData?.Platform}, {playerData?.UserName}, {playerData?.Identifier}): " + exc.ToString());
                return result;
            }
            finally
            {
                if (result.Success)
                {
                    ravenbotApi.UpdateUserSettings(result.Player.UserId);
                }
#if DEBUG
                if (!result.Success && session != null)
                {
                    var sessionOwner = gameData.GetUser(session.UserId);
                    logger.LogError(("Unable to add player " + playerData.UserName + " to " + sessionOwner.UserName + "'s stream. " + (result.ErrorMessage + " " + internalErrorMessage).Trim()).Trim());
                }

#endif
            }
        }

        private async Task SendUserRoleToRavenBotAsync(User user)
        {
            if (user == null || (!user.IsModerator.GetValueOrDefault() && !user.IsAdmin.GetValueOrDefault()))
            {
                return;
            }

            ravenbotApi.UpdateUserSettings(user.Id);
        }

        public async Task<PlayerJoinResult> AddPlayerByCharacterId(DataModels.GameSession session, Guid characterId, bool isGameRestore = false)
        {
            var result = new PlayerJoinResult();
            var c = gameData.GetCharacter(characterId);
            if (c == null)
            {
                result.ErrorMessage = $"No character found using id '{characterId}'";
                return result;
            }

            if (session == null)
            {
                result.ErrorMessage = $"Session has been terminated";
                return result;
            }

            var u = gameData.GetUser(c.UserId);
            if (u == null)
            {
                result.ErrorMessage = $"No user found with id '{c.UserId}'";
                return result;
            }

            if (u.Status.GetValueOrDefault() == (int)AccountStatus.TemporarilySuspended)
            {
                result.Success = false;
                result.ErrorMessage = "You have been temporarily suspended from playing. Contact the staff for more information.";
                return result;
            }

            if (u.Status.GetValueOrDefault() == (int)AccountStatus.PermanentlySuspended)
            {
                result.Success = false;
                result.ErrorMessage = "You have been permanently suspended from playing. Contact the staff for more information.";
                return result;
            }


#if DEBUG
            var sessionOwner = gameData.GetUser(session.UserId);
            if (!sessionOwner.IsAdmin.GetValueOrDefault() && isGameRestore && (c.UserIdLock != null && c.UserIdLock != session.UserId))
#else
            if (isGameRestore && (c.UserIdLock != null && c.UserIdLock != session.UserId))
#endif
            {
                var targetUser = gameData.GetUser(c.UserIdLock.GetValueOrDefault());
                result.Success = false;
                if (targetUser != null)
                {
                    result.ErrorMessage = "Player has left to join " + targetUser.UserName + "'s stream.";
                }
                else
                {
                    result.ErrorMessage = "Player has left to join another stream.";
                }
                return result;
            }

            result.Player = await AddPlayerToSession(session, u, c);
            result.Success = result.Player != null;
            return result;
        }

        public async Task<PlayerRestoreResult> RestorePlayersToGame(SessionToken sessionToken, PlayerRestoreData players)
        {
            var result = new PlayerRestoreResult();
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session == null || session.Status != (int)SessionStatus.Active)
            {
                result.ErrorMessage = "Session is unavailable";
                return result;
            }

            try
            {
                result.Success = true;
                var chars = players.Characters ?? Array.Empty<Guid>();
                var charactersToAdd = new HashSet<Guid>(players.Characters);
                var currentCharacters = gameData.GetActiveSessionCharacters(session);
                foreach (var c in currentCharacters)
                {
                    if (charactersToAdd.Contains(c.Id))
                    {
                        continue;
                    }
                    await RemovePlayerFromActiveSession(session, c.Id);
                }

                if (chars.Length == 0)
                {
                    result.Players = Array.Empty<PlayerJoinResult>();
                    return result;
                }

                result.Players = new PlayerJoinResult[players.Characters.Length];
                for (int i = 0; i < players.Characters.Length; i++)
                {
                    result.Players[i] = await AddPlayerByCharacterId(session, players.Characters[i], true);
                }
            }
            catch (Exception exc)
            {
                var sessionOwner = gameData.GetUser(session.UserId);
                logger.LogError("Restoring players failed. (Session: " + session.Id + ", " + sessionOwner?.UserName + ") " + exc?.Message + " " + exc?.InnerException?.Message);

                result.Success = false;
                result.ErrorMessage = "Restoring players failed. Server encountered an error.";
            }

            return result;
        }

        public async Task<PlayerJoinResult> AddPlayer(SessionToken token, PlayerJoinData playerData)
        {
            var result = new PlayerJoinResult();
            var session = gameData.GetSession(token.SessionId);
            if (session == null || session.Status != (int)SessionStatus.Active)
            {
                result.ErrorMessage = "Session is unavailable";
                return result;
            }

            return await AddPlayer(session, playerData);

        }

        public Task<Player> AddPlayer(SessionToken token, Guid characterId)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null || session.Status != (int)SessionStatus.Active)
            {
                return null;
            }
            var character = gameData.GetCharacter(characterId);
            var user = gameData.GetUser(character.UserId);
            return AddPlayerToSession(session, user, character);
        }

        private async Task<Player> AddPlayerToSession(DataModels.GameSession session, User user, Character character)
        {
            try
            {
                if (character == null)
                {
                    logger.LogError("AddPlayerToSession failed; character " + user?.UserName + ", " + character?.Identifier + " is null.");
                    return null;
                }

                if (user == null)
                {
                    logger.LogError("AddPlayerToSession failed; user " + character?.Name + ", " + character?.Identifier + " is null.");
                    return null;
                }

                if (session == null)
                {
                    logger.LogError("AddPlayerToSession failed; session is null. user " + character?.Name + ", " + character?.Identifier);
                    return null;
                }

                if (user.Status.GetValueOrDefault() == (int)AccountStatus.TemporarilySuspended)
                {
                    return null;
                }

                if (user.Status.GetValueOrDefault() == (int)AccountStatus.PermanentlySuspended)
                {
                    return null;
                }
                var rejoin = false;
                // check if we need to remove the player from
                // their active session.

                var sessionChars = gameData.GetActiveSessionCharacters(session);
                if (sessionChars != null && sessionChars.Count > 0)
                {
                    // get the characters from the same user, in case they have another character already on this stream.
                    // set them all to userIdLock = null. But also only if it is not the character we are joining with.
                    var charactersInSession = sessionChars.Where(x => x.UserId == user.Id && x.Id != character.Id).ToList();
                    foreach (var cs in charactersInSession)
                    {
                        cs.UserIdLock = null;
                    }
                }

                rejoin = character.UserIdLock == session.UserId;

#if DEBUG
                if (character.UserIdLock != null && !rejoin)
                {
                    var prevUser = gameData.GetUser(character.UserIdLock.Value);
                    var newUser = gameData.GetUser(session.UserId);
                    logger.LogDebug(character.Name + " left " + prevUser.UserName + " to join " + newUser.UserName);
                }
#endif
                character.UserIdLock = session.UserId;

                if (character.UserIdLock != null)
                {
                    SendRemovePlayerFromSessionToGame(character, session);
                }

                var app = gameData.GetAppearance(character.SyntyAppearanceId);

                if (app == null)
                {
                    // player not properly initialized.

                    app = GenerateRandomSyntyAppearance();
                    character.SyntyAppearanceId = app.Id;
                    gameData.Add(app);
                }

                var clanMembership = gameData.GetClanMembership(character.Id);
                if (clanMembership != null)
                {
                    var role = gameData.GetClanRole(clanMembership.ClanRoleId);
                    if (role != null && app != null && app.Cape == -1)
                    {
                        app.Cape = role.Cape;
                    }
                }
                else if (app != null)
                {
                    app.Cape = -1;
                }

                character.LastUsed = DateTime.UtcNow;

                if (character.StateId != null)
                {
                    var state = gameData.GetCharacterState(character.StateId);
                    if (state != null && state.Island == "war")
                    {
                        state.Island = null;
                        state.X = null;
                        state.Y = null;
                        state.Z = null;
                    }
                }

                await SendUserRoleToRavenBotAsync(user);

                await this.TrySendToExtensionAsync(session, character, new PlayerAdd
                {
                    Identifier = character.Identifier,
                    UserId = user.Id,
                    UserName = user.UserName,
                    CharacterId = character.Id
                });

                gameData.ResetCharacterSessionState(session.Id, character.Id);

                return character.Map(gameData, user, rejoin, true);
            }
            catch (Exception exc)
            {
                logger.LogError("AddPlayerToSession failed; character " + user?.UserName + ", " + character?.Identifier + ", session id " + session?.Id + ", session owner id " + session?.UserId + "; " + exc);
                return null;
            }
        }

        public Task<bool> RemovePlayerFromActiveSession(SessionToken token, Guid characterId)
        {
            var session = gameData.GetSession(token.SessionId);
            return RemovePlayerFromActiveSession(session, characterId);
        }

        public async Task<bool> RemovePlayerFromActiveSession(DataModels.GameSession session, Guid characterId)
        {
            if (session == null) return false;
            var character = gameData.GetCharacter(characterId);
            if (character == null) return false;

            var user = gameData.GetUser(character.UserId);
            if (user == null) return false;
            var sessionOwner = gameData.GetUser(session.UserId);
            if (sessionOwner == null) return false;
            if (sessionOwner.Id != character.UserIdLock)
                return false;

            await this.TrySendToExtensionAsync(session, character, new PlayerRemove
            {
                CharacterId = characterId,
                Reason = "Left Game",
            });

            character.UserIdLock = null;

#if DEBUG
            logger.LogDebug(character.Name + " removed from " + sessionOwner.UserName + "'s session.");
#endif
            return true;
        }

        public async Task RemoveUserFromSessions(User user)
        {
            if (user == null)
            {
                return;
            }

            var characters = gameData.GetCharactersByUserId(user.Id);
            if (characters == null || characters.Count == 0)
            {
                return;
            }

            var reason = "Forcibly removed from the game.";
            foreach (var character in characters)
            {
                if (character.UserIdLock == null)
                    continue;

                var currentSession = gameData.GetSessionByUserId(character.UserIdLock.GetValueOrDefault());
                if (currentSession == null)
                    continue;

                var sessionOwner = gameData.GetUser(currentSession.UserId);

#if DEBUG
                logger.LogDebug(character.Name + " removed from " + sessionOwner.UserName + "'s session.");
#endif

                character.UserIdLock = null;
                var gameEvent = gameData.CreateSessionEvent(GameEventType.PlayerRemove,
                 currentSession,
                 new PlayerRemove()
                 {
                     Reason = reason,
                     UserId = user.Id,
                     CharacterId = character.Id
                 });

                gameData.EnqueueGameEvent(gameEvent);

                await this.TrySendToExtensionAsync(character, new PlayerRemove
                {
                    CharacterId = character.Id,
                    Reason = "Removed from game",
                });
            }
        }

        public bool SendRemovePlayerFromSessionToGame(Character character, DataModels.GameSession joiningSession = null)
        {
            var userToRemove = gameData.GetUser(character.UserId);
            if (userToRemove == null)
                return false;

            var currentSession = gameData.GetSessionByUserId(character.UserIdLock.GetValueOrDefault());
            if (currentSession == null)
                return false;

            if (joiningSession != null && (currentSession.Id == joiningSession.Id || currentSession.UserId == joiningSession.UserId))
                return false;

            string reason;
            if (joiningSession != null)
            {
                var targetSessionUser = gameData.GetUser(joiningSession.UserId);
                reason = $"{character.Name} joined {targetSessionUser.UserName}'s stream";
            }
            else
            {
                reason = "Left the game using the extension.";
            }

            var characterUser = gameData.GetUser(character.UserId);
            var gameEvent = gameData.CreateSessionEvent(GameEventType.PlayerRemove,
                currentSession,
                new PlayerRemove()
                {
                    Reason = reason,
                    UserId = characterUser.Id,
                    CharacterId = character.Id
                });

            gameData.EnqueueGameEvent(gameEvent);
            return true;
        }

        public Player GetPlayer(Guid userId, string identifier)
        {
            var user = gameData.GetUser(userId);
            if (user == null)
            {
                return null;
            }

            return GetPlayerByUser(user, identifier);
        }

        public bool AddTokens(SessionToken sessionToken, string userId, int amount)
        {
            var character = GetCharacter(sessionToken, userId);
            if (character == null)
                return false;

            var session = gameData.GetSession(sessionToken.SessionId);
            var inventory = inventoryProvider.Get(character.Id);
            inventory.AddStreamerTokens(session, amount);
            return true;
        }

        public bool AddTokens(SessionToken sessionToken, Guid characterId, int amount)
        {
            var character = GetCharacter(sessionToken, characterId);
            if (character == null)
                return false;

            var session = gameData.GetSession(sessionToken.SessionId);
            var inventory = inventoryProvider.Get(character.Id);
            inventory.AddStreamerTokens(session, amount);
            return true;
        }

        public RedeemItemResult RedeemItem(SessionToken sessionToken, Guid characterId, Guid itemId)
        {
            try
            {
                var character = GetCharacter(sessionToken, characterId);
                if (character == null)
                {
                    logger.LogError("Unable to redeem item for " + characterId + ". Character not found in session: " + sessionToken.SessionId);
                    return RedeemItemResult.Error("Player not in session.");
                }

                var session = gameData.GetSession(sessionToken.SessionId);
                var inventory = inventoryProvider.Get(character.Id);
                var redeemable = gameData.GetRedeemableItemByItemId(itemId);
                var item = gameData.GetItem(itemId);
                if (redeemable == null)
                {
                    if (item != null)
                    {
                        return RedeemItemResult.NoSuchItem(item.Name);
                    }

                    return RedeemItemResult.NoSuchItem();
                }

                if (!ValidateDateRange(redeemable))
                {
                    return RedeemItemResult.NoSuchItem(item.Name);
                }

                var stashCurrencyItem = gameData.GetStashItem(character.UserId, redeemable.CurrencyItemId);
                var stashCurrencyAmount = stashCurrencyItem != null ? stashCurrencyItem.Amount : 0;
                var currencyItem = inventory.GetByItemId(redeemable.CurrencyItemId);
                if ((currencyItem.Amount + stashCurrencyAmount) < redeemable.Cost)
                {
                    var insufficient = RedeemItemResult.InsufficientCurrency(currencyItem.Amount, redeemable.Cost, currencyItem.Item.Name);
                    insufficient.CurrencyItemId = redeemable.CurrencyItemId;
                    insufficient.RedeemedItemId = redeemable.ItemId;
                    insufficient.CurrencyLeft = currencyItem.Amount;
                    insufficient.CurrencyCost = redeemable.Cost;
                    return insufficient;
                }

                if (inventory.RemoveItem(currencyItem, redeemable.Cost, out var toRemove))
                {
                    if (toRemove > 0)
                    {
                        if (stashCurrencyItem != null)
                        {
                            gameData.RemoveFromStash(stashCurrencyItem, (int)toRemove);
                        }
                        else
                        {
                            logger.LogError("Redeem Bug: Unable to remove items from stash (Res Id: " + redeemable.CurrencyItemId + ", Char Id: " + character.UserId + ", Amount: " + toRemove + ")");
                        }
                    }

                    inventory.AddItem(redeemable.ItemId, Math.Max(1, redeemable.Amount));
                }

                if (currencyItem.Amount > 0)
                {
                    SendItemRemoveEvent(new DataModels.InventoryItem
                    {
                        ItemId = redeemable.CurrencyItemId,
                    }, redeemable.Cost > currencyItem.Amount ? currencyItem.Amount : redeemable.Cost, character);
                }

                SendItemAddEvent(new DataModels.InventoryItem { ItemId = redeemable.ItemId, Soulbound = false }, redeemable.Amount, character);

                return new RedeemItemResult
                {
                    Code = RedeemItemResultCode.Success,
                    RedeemedItemAmount = redeemable.Amount,
                    RedeemedItemId = redeemable.ItemId,
                    CurrencyItemId = redeemable.CurrencyItemId,
                    CurrencyCost = redeemable.Cost,
                    CurrencyLeft = inventory.GetByItemId(redeemable.CurrencyItemId).Amount,
                };
            }
            catch (Exception exc)
            {
                return RedeemItemResult.Error("Unknown Error");
            }
        }

        public WebsitePlayer GetWebsitePlayer(Guid userId, string identifier)
        {
            var user = gameData.GetUser(userId);
            if (user == null)
            {
                return null;
            }

            return GetWebsitePlayer(user, identifier);
        }

        public WebsitePlayer GetWebsitePlayer(Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null) return null;
            var user = gameData.GetUser(character.UserId);
            if (user == null) return null;
            return GetWebsitePlayer(user, character);
        }

        public WebsitePlayer GetWebsitePlayer(string userId, string identifier)
        {
            return GetWebsitePlayer(gameData.GetUserByTwitchId(userId), identifier);
        }

        public IReadOnlyList<WebsitePlayer> GetWebsitePlayers(Guid userId)
        {
            return GetWebsitePlayers(gameData.GetUser(userId));
        }

        public IReadOnlyList<WebsitePlayer> GetWebsitePlayers(string userId)
        {
            return GetWebsitePlayers(gameData.GetUserByTwitchId(userId));
        }

        private IReadOnlyList<WebsitePlayer> GetWebsitePlayers(User user)
        {
            if (user == null) return null;
            var userChars = gameData.GetCharacters(x => x.UserId == user.Id).OrderBy(x => x.CharacterIndex);
            var result = new List<WebsitePlayer>();
            foreach (var c in userChars)
                result.Add(GetWebsitePlayer(user, c));
            return result;
        }

        /// <summary>
        /// Gets a mapped Player without Inventory
        /// </summary>
        /// <param name="characterId"></param>
        /// <returns></returns>
        public Player GetPlayerInfo(Guid characterId)
        {
            var chara = gameData.GetCharacter(characterId);
            if (chara == null) return null;
            var user = gameData.GetUser(chara.UserId);
            var player = chara.Map(gameData, user);
            player.InventoryItems = new List<RavenNest.Models.InventoryItem>();
            return player;
        }

        public Player GetPlayer(Guid characterId)
        {
            var chara = gameData.GetCharacter(characterId);
            if (chara == null) return null;
            var user = gameData.GetUser(chara.UserId);
            return chara.Map(gameData, user);
        }


        public Player GetPlayer(string userId, string platform, string identifier, bool includeInventoryItems = true)
        {
            if (Guid.TryParse(userId, out var characterId))
            {
                var character = gameData.GetCharacter(characterId);
                if (character != null)
                {
                    var user = gameData.GetUser(character.UserId);
                    if (user == null) return null;
                    var player = character.Map(gameData, user);
                    if (!includeInventoryItems)
                        player.InventoryItems = new List<RavenNest.Models.InventoryItem>();
                    return player;
                }
                return null;
            }
            else
            {
                var user = gameData.GetUserByTwitchId(userId);
                if (user == null)
                {
                    return null;
                }
                var player = GetPlayerByUser(user, identifier);
                if (!includeInventoryItems)
                    player.InventoryItems = new List<RavenNest.Models.InventoryItem>();
                return player;
            }
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

            var sessionCharacters = gameData.GetActiveSessionCharacters(session);
            var character = sessionCharacters.FirstOrDefault(x => x.UserId == user.Id);
            if (character == null)
            {
                character = gameData.GetCharacterByUserId(user.Id, "1");
            }

            return character.Map(gameData, user);
        }

        public bool LoyaltyGift(string gifterTwitchUserId, string streamerTwitchUserIdOrName, int bitsAmount, int subsAmount)
        {
            var cheerer = gameData.GetUserByUsername(gifterTwitchUserId);
            if (cheerer == null || (bitsAmount <= 0 && subsAmount <= 0))
                return false;

            var streamer = gameData.GetUserByUsername(streamerTwitchUserIdOrName);
            if (streamer == null)
                return false;

            var loyalty = gameData.GetUserLoyalty(cheerer.Id, streamer.Id);
            if (loyalty == null)
                loyalty = CreateUserLoyalty(cheerer.Id, streamer.Id);
            if (bitsAmount > 0)
                loyalty.AddCheeredBits(bitsAmount);
            if (subsAmount > 0)
                loyalty.AddGiftedSubs(subsAmount);

            return true;
        }

        public bool AddLoyaltyData(SessionToken sessionToken, LoyaltyUpdate data)
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session == null)
                return false;

            //var joinData = new PlayerJoinData
            //{
            //    UserId = data.UserId,
            //    UserName = data.UserName,
            //    Identifier = "0",
            //};

            var user = gameData.GetUserByTwitchId(data.UserId);
            if (user == null)
            {
                return false;
                //user = CreateUser(session, joinData);
                //return false;
            }

            var loyalty = gameData.GetUserLoyalty(user.Id, session.UserId);
            if (loyalty == null)
            {
                loyalty = CreateUserLoyalty(session, user);
            }

            if (data.SubsCount > 0) loyalty.AddGiftedSubs(data.SubsCount);
            if (data.BitsCount > 0) loyalty.AddCheeredBits(data.BitsCount);
            return true;
        }

        public void UpdateUserLoyalty(SessionToken sessionToken, UserLoyaltyUpdate update)
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session == null)
                return;

            var character = gameData.GetCharacter(update.CharacterId);
            if (character == null) return;

            var user = gameData.GetUser(character.UserId);
            if (user == null)
            {
                //user = CreateUser(session, playerData);
                return;
            }

            var playerData = new PlayerJoinData
            {
                Vip = update.IsVip,
                PlatformId = update.UserId,
                Identifier = "0",
                Moderator = update.IsModerator,
                Subscriber = update.IsSubscriber
            };

            var loyalty = gameData.GetUserLoyalty(user.Id, session.UserId);
            if (loyalty == null)
            {
                loyalty = CreateUserLoyalty(session, user, playerData);
            }

            loyalty.IsModerator = update.IsModerator;
            loyalty.IsSubscriber = update.IsSubscriber;
            loyalty.IsVip = update.IsVip;
            if (update.NewGiftedSubs > 0)
                loyalty.AddGiftedSubs(update.NewGiftedSubs);
            if (update.NewCheeredBits > 0)
                loyalty.AddCheeredBits(update.NewCheeredBits);
        }

        public void UpdatePlayerActivity(SessionToken sessionToken, PlayerSessionActivity update)
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session == null)
                return;

            var character = gameData.GetCharacter(update.CharacterId);
            if (character == null)
                return;

            var user = gameData.GetUser(character.UserId);
            if (user == null)
                return;

            var sessionActivity = gameData.GetSessionActivity(session.Id, update.CharacterId);
            if (sessionActivity == null)
            {
                sessionActivity = new CharacterSessionActivity
                {
                    Id = Guid.NewGuid(),
                    SessionId = session.Id,
                    UserId = user.Id,
                    UserName = update.UserName,
                    CharacterId = update.CharacterId,
                    Created = DateTime.UtcNow,
                };
                gameData.Add(sessionActivity);
            }

            sessionActivity.TripCount = update.TripCount;
            sessionActivity.Tripped = update.Tripped;
            sessionActivity.TotalTriggerCount = update.TotalTriggerCount;
            sessionActivity.TotalInputCount = update.TotalInputCount;
            sessionActivity.ResponseStreak = update.ResponseStreak;
            sessionActivity.MinResponseTime = update.MinResponseTime.ToString();
            sessionActivity.MaxResponseTime = update.MaxResponseTime.ToString();
            sessionActivity.AvgResponseTime = update.AvgResponseTime.ToString();
            sessionActivity.MaxResponseStreak = update.MaxResponseStreak;
            sessionActivity.Updated = DateTime.UtcNow;
        }


        public Player GetPlayer(SessionToken sessionToken, string twitchUserId)
        {
            var user = gameData.GetUserByTwitchId(twitchUserId);
            if (user == null)
            {
                return null;
            }

            var session = gameData.GetSession(sessionToken.SessionId);
            if (session == null)
            {
                return null;
            }

            var sessionCharacters = gameData.GetActiveSessionCharacters(session);
            var character = sessionCharacters.FirstOrDefault(x => x.UserId == user.Id);
            if (character == null)
            {
                character = gameData.GetCharacterByUserId(user.Id, "1");
            }

            return character.Map(gameData, user);
        }

        public bool UpdateAppearance(
            SessionToken token, string userId, RavenNest.Models.SyntyAppearance appearance)
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
            RavenNest.Models.SyntyAppearance appearance)
        {

            var character = gameData.GetCharacterByUserId(token.UserId, identifier);
            var control = gameData.GetCharacterByUserId(userId, identifier);
            if (character == null || control.Id != character.Id)
            {
                return false;
            }

            return UpdateAppearance(userId, identifier, appearance);
        }

        public ItemEnchantmentResult EnchantItemInstance(SessionToken sessionToken, string userId, Guid inventoryItemId)
        {
            return EnchantItem(sessionToken, userId, inventoryItemId);
        }

        public ItemEnchantmentResult EnchantItemInstance(SessionToken sessionToken, Guid characterId, Guid inventoryItemId)
        {
            var character = GetCharacter(sessionToken, characterId);

            return EnchantItem(sessionToken, inventoryItemId, character);
        }

        public ItemEnchantmentResult EnchantItem(SessionToken token, string userId, Guid inventoryItemId)
        {
            var character = GetCharacter(token, userId);

            return EnchantItem(token, inventoryItemId, character);
        }

        private ItemEnchantmentResult EnchantItem(SessionToken token, Guid inventoryItemId, Character character)
        {
            var enchantingSkill = gameData.GetSkills().FirstOrDefault(x => x.Name == "Enchanting");
            if (character == null || enchantingSkill == null)
                return ItemEnchantmentResult.Error();

            var user = gameData.GetUser(character.UserId);

            if (user == null)
                return ItemEnchantmentResult.Error();

            if (!integrityChecker.VerifyPlayer(token.SessionId, character.Id, 0))
                return ItemEnchantmentResult.Error();

            var resources = gameData.GetResources(character);
            if (resources == null)
                return ItemEnchantmentResult.Error();

            var clanMembership = gameData.GetClanMembership(character.Id);
            if (clanMembership == null)
                return ItemEnchantmentResult.NotAvailable();

            var skills = gameData.GetClanSkills(clanMembership.ClanId);
            if (skills == null || skills.Count == 0)
                return ItemEnchantmentResult.NotAvailable();

            var clanSkill = skills.FirstOrDefault(x => x.SkillId == enchantingSkill.Id);

            if (clanSkill == null)
                return ItemEnchantmentResult.NotAvailable();

            var inventory = inventoryProvider.Get(character.Id);
            var item = inventory.Get(inventoryItemId);

            if (item.IsNull())
                return ItemEnchantmentResult.Error();

            return enchantmentManager.EnchantItem(token.SessionId, clanSkill, character, inventory, item, resources);
        }

        [Obsolete]
        public ItemEnchantmentResult DisenchantItemInstance(SessionToken sessionToken, string userId, Guid inventoryItemId)
        {
            var character = GetCharacter(sessionToken, userId);
            return DisenchantItem(sessionToken, inventoryItemId, character);
        }

        public ItemEnchantmentResult DisenchantItemInstance(SessionToken sessionToken, Guid characterId, Guid inventoryItemId)
        {
            var character = GetCharacter(sessionToken, characterId);
            return DisenchantItem(sessionToken, inventoryItemId, character);
        }

        private ItemEnchantmentResult DisenchantItem(SessionToken sessionToken, Guid inventoryItemId, Character character)
        {
            var enchantingSkill = gameData.GetSkills().FirstOrDefault(x => x.Name == "Enchanting");
            if (character == null || enchantingSkill == null)
                return ItemEnchantmentResult.Error();

            var user = gameData.GetUser(character.UserId);

            if (user == null)
                return ItemEnchantmentResult.Error();

            if (!integrityChecker.VerifyPlayer(sessionToken.SessionId, character.Id, 0))
                return ItemEnchantmentResult.Error();

            var inventory = inventoryProvider.Get(character.Id);
            var item = inventory.Get(inventoryItemId);

            if (item.IsNull())
                return ItemEnchantmentResult.Error();

            return enchantmentManager.DisenchantItem(sessionToken.SessionId, character, inventory, item);
        }

        public CraftItemResult CraftItems(SessionToken token, Guid characterId, Guid itemId, int amount)
        {
            var character = GetCharacter(token, characterId);
            if (character == null || !integrityChecker.VerifyPlayer(token.SessionId, character.Id, 0))
                return CraftItemResult.Error;
            return CraftItems(token, itemId, ref amount, character);
        }

        public CraftItemResult CraftItems(SessionToken token, string userId, Guid itemId, int amount)
        {
            var character = GetCharacter(token, userId);
            if (character == null || !integrityChecker.VerifyPlayer(token.SessionId, character.Id, 0))
                return CraftItemResult.Error;
            return CraftItems(token, itemId, ref amount, character);
        }

        private CraftItemResult CraftItems(SessionToken token, Guid itemId, ref int amount, Character character)
        {
            if (amount <= 0) amount = 1;
            amount = Math.Min(50_000_000, amount);

            var item = gameData.GetItem(itemId);
            if (item == null) return CraftItemResult.NoSuchItem;

            var resources = gameData.GetResources(character);
            var skills = gameData.GetCharacterSkills(character.SkillsId);
            if (skills == null || resources == null)
                return CraftItemResult.Error;

            var craftingBonus = GetCraftingBonus(character);
            var craftingLevel = skills.CraftingLevel + craftingBonus;

            if (craftingLevel < item.RequiredCraftingLevel)
                return CraftItemResult.TooLowLevel(item.Id, item.RequiredCraftingLevel);

            if (!CanCraftItems(item, resources, craftingLevel, amount, out var craftableAmount))
                return CraftItemResult.InsufficientResources;

            var result = new CraftItemResult();
            var craftingRequirements = gameData.GetCraftingRequirements(itemId);
            var inventory = inventoryProvider.Get(character.Id);

            foreach (var requirement in craftingRequirements)
            {
                var stashItem = gameData.GetStashItem(character.UserId, requirement.ResourceItemId);
                var availableResxAmount = stashItem != null ? stashItem.Amount : 0;

                var invItem = inventory.GetUnequippedItem(requirement.ResourceItemId);
                if (invItem.IsNull() && availableResxAmount == 0)
                {
                    return CraftItemResult.InsufficientResources;
                }

                availableResxAmount += invItem.Amount;
                var maxCraftable = availableResxAmount / requirement.Amount;
                if (craftableAmount > maxCraftable)
                {
                    craftableAmount = (int)maxCraftable;
                }

                if (craftableAmount == 0)
                {
                    return CraftItemResult.InsufficientResources;
                }
            }

            if (craftableAmount == 0)
            {
                return CraftItemResult.InsufficientResources;
            }

            foreach (var requirement in craftingRequirements)
            {
                var stashItem = gameData.GetStashItem(character.UserId, requirement.ResourceItemId);
                var availableAmount = stashItem != null ? stashItem.Amount : 0;
                var resx = inventory.GetUnequippedItem(requirement.ResourceItemId);

                availableAmount += resx.Amount;
                var requiredAmount = requirement.Amount * craftableAmount;

                if (availableAmount < requiredAmount)
                {
                    return CraftItemResult.InsufficientResources;
                }

                if (resx.Amount <= 0)
                {
                    if (!gameData.RemoveFromStash(stashItem, requiredAmount))
                    {
                        return CraftItemResult.InsufficientResources;
                    }
                }
                else
                {
                    var invItemRemoved = inventory.RemoveItem(resx, requiredAmount, out var leftToRemove);
                    if (stashItem == null && !invItemRemoved && leftToRemove == requiredAmount)
                    {
                        return CraftItemResult.InsufficientResources;
                    }

                    if (leftToRemove > 0)
                    {
                        if (!gameData.RemoveFromStash(stashItem, (int)leftToRemove))
                        {
                            var resItem = gameData.GetItem(requirement.ResourceItemId);
                            logger.LogError("Crafting Bug: Unable to remove items from stash (Res Id: " + requirement.ResourceItemId + " [" + resItem.Name + "], Char Id: " + character.Id + " [" + character.Name + "], Amount: " + leftToRemove + ") "
                                + " Amount in Stash: " + (stashItem?.Amount ?? 0));
                        }
                    }
                }
            }

            resources.Wood -= item.WoodCost * craftableAmount;
            resources.Ore -= item.OreCost * craftableAmount;
            DataModels.InventoryItem itemStack = null;
            for (var i = 0; i < craftableAmount; ++i)
            {
                AddItem(token, character.Id, itemId, out itemStack);
            }
            result.Value = craftableAmount;
            result.Status = craftableAmount == amount ? CraftItemResultStatus.Success : CraftItemResultStatus.PartialSuccess;
            result.InventoryItemId = itemStack.Id;
            result.ItemId = itemId;
            return result;
        }

        private int GetCraftingBonus(Character character)
        {
            var inventory = inventoryProvider.Get(character.Id);
            var equipped = inventory.GetEquippedItems();

            var skills = ModelMapper.MapForWebsite(gameData.GetCharacterSkills(character.SkillsId));
            var playerSkills = skills.AsList();
            var craftingBonus = 0d;
            foreach (var item in equipped)
            {
                var bonuses = item.GetSkillBonuses(playerSkills, gameData);
                foreach (var bonus in bonuses)
                {
                    if (bonus.Skill.Name == "Crafting")
                    {
                        craftingBonus += bonus.Bonus;
                    }
                }
            }
            return (int)craftingBonus;
        }

        private static bool CanCraftItems(Item item, Resources resources, int craftingLevel, int amount, out int maxAmount)
        {
            maxAmount = 0;
            if (!item.Craftable)
                return false;

            if (item.RequiredCraftingLevel > craftingLevel)
                return false;

            if (resources.Wood < item.WoodCost)
                return false;

            if (resources.Ore < item.OreCost)
                return false;

            maxAmount = Math.Min(
                item.WoodCost > 0 ? (int)(resources.Wood / item.WoodCost) : amount,
                item.OreCost > 0 ? (int)(resources.Ore / item.OreCost) : amount);

            return maxAmount > 0;
        }

        public void AddItem(Guid characterId, Guid itemId, int amount = 1)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null)
                return;

            var inventory = inventoryProvider.Get(characterId);
            var items = inventory.AddItem(itemId, amount);
            SendItemAddEvent(items[0], amount, character);
        }
        public bool ReturnMarketplaceItem(DataModels.MarketItem item)
        {
            if (item == null) return false;
            var character = gameData.GetCharacter(item.SellerCharacterId);
            if (character == null) return false;

            // Add the item back to the inventory
            var itemId = item.ItemId;
            var amount = item.Amount;
            //var enchantment = item.Enchantment;
            var characterId = character.Id;
            var inventory = inventoryProvider.Get(characterId);
            var inventoryItems = inventory.AddItem(itemId, amount);

            // Remove the item from the marketplace
            gameData.Remove(item);

            // Send the update to the game client and extensions
            SendItemAddEvent(inventoryItems[0], (int)amount, character);

            return true;
        }

        private void SendItemEquipEvent(Guid inventoryItemId, bool equipped, Character character)
        {
            var sessionUserId = character.UserIdLock;
            if (sessionUserId == null)
                return;

            var data = new ItemEquip
            {
                PlayerId = character.Id,
                InventoryItemId = inventoryItemId,
                IsEquipped = equipped
            };

            var session = gameData.GetSessionByUserId(sessionUserId.Value);
            if (session != null)
            {
                gameData.EnqueueGameEvent(gameData.CreateSessionEvent(equipped ? GameEventType.ItemEquip : GameEventType.ItemUnEquip, session, data));
                TrySendToExtensionAsync(character, data);
            }
        }

        private void SendItemAddEvent(DataModels.InventoryItem item, long amount, Character character)
        {
            var sessionUserId = character.UserIdLock;
            if (sessionUserId == null)
                return;

            var data = new ItemAdd
            {
                PlayerId = character.Id,
                Amount = amount,
                ItemId = item.ItemId,
                InventoryItemId = item.Id,
                Name = item.Name,
                Enchantment = item.Enchantment,
                TransmogrificationId = item.TransmogrificationId,
                Flags = item.Flags.GetValueOrDefault(),
                Tag = item.Tag,
                Soulbound = item.Soulbound.GetValueOrDefault(),
            };

            var session = gameData.GetSessionByUserId(sessionUserId.Value);
            if (session != null)
            {
                gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.ItemAdd, session, data));
                TrySendToExtensionAsync(character, data);
            }
        }

        private async void SendItemRemoveEvent(DataModels.GameSession session, DataModels.InventoryItem item, long amount, Character character)
        {
            var data = new ItemRemove
            {
                PlayerId = character.Id,
                Amount = amount,
                ItemId = item.ItemId,
                InventoryItemId = item.Id
            };

            gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.ItemRemove, session, data));
            await TrySendToExtensionAsync(character, data);
        }

        private async void SendItemRemoveEvent(DataModels.InventoryItem item, long amount, Character character, bool sendToGame = false)
        {
            var data = new ItemRemove
            {
                PlayerId = character.Id,
                Amount = amount,
                ItemId = item.ItemId,
                InventoryItemId = item.Id
            };

            if (sendToGame)
            {
                var sessionUserId = character.UserIdLock;
                if (sessionUserId != null)
                {
                    var session = gameData.GetSessionByUserId(sessionUserId.Value);
                    if (session != null)
                    {
                        gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.ItemRemove, session, data));
                    }
                }
            }

            await TrySendToExtensionAsync(character, data);
        }



        public AddItemInstanceResult AddItemInstanceDetailed(SessionToken token, string userId, RavenNest.Models.InventoryItem instance)
        {
            var character = GetCharacter(token, userId);
            if (character == null)
                return AddItemInstanceResult.BadCharacter(userId);

            return AddItemToCharacter(token, instance, character);
        }

        public AddItemInstanceResult AddItem(SessionToken token, Guid characterId, RavenNest.Models.InventoryItem instance)
        {
            var character = GetCharacter(token, characterId);
            if (character == null)
                return AddItemInstanceResult.Failed();

            return AddItemToCharacter(token, instance, character);
        }

        private AddItemInstanceResult AddItemToCharacter(SessionToken token, RavenNest.Models.InventoryItem instance, Character character)
        {
            var item = gameData.GetItem(instance.ItemId);
            if (item == null)
                return AddItemInstanceResult.NoSuchItem(instance.ItemId);


            if (!integrityChecker.VerifyPlayer(token.SessionId, character.Id, 0))
                return AddItemInstanceResult.Failed();

            var session = gameData.GetSession(token.SessionId);
            if (session == null)
                return AddItemInstanceResult.NoSuchSession();

            var sessionOwner = gameData.GetUser(session.UserId);
            if (sessionOwner == null || sessionOwner.Status >= 1)
                return AddItemInstanceResult.NoSuchSession();

            string tag = null;
            if (item.Category == (int)DataModels.ItemCategory.StreamerToken)
                tag = sessionOwner.UserId;

            var inventory = inventoryProvider.Get(character.Id);
            var addedItem = inventory.AddItemInstance(instance, 1);
            //inventory.EquipBestItems();

            return AddItemInstanceResult.ItemAdded(addedItem.Id);
        }

        public Guid AddItemInstance(SessionToken token, string userId, RavenNest.Models.InventoryItem instance)
        {
            var item = gameData.GetItem(instance.ItemId);
            if (item == null)
                return Guid.Empty;

            var character = GetCharacter(token, userId);
            if (character == null)
                return Guid.Empty;

            if (!integrityChecker.VerifyPlayer(token.SessionId, character.Id, 0))
                return Guid.Empty;

            var session = gameData.GetSession(token.SessionId);
            if (session == null)
                return Guid.Empty;

            var sessionOwner = gameData.GetUser(session.UserId);
            if (sessionOwner == null || sessionOwner.Status >= 1)
                return Guid.Empty;

            string tag = null;
            if (item.Category == (int)DataModels.ItemCategory.StreamerToken)
                tag = sessionOwner.UserId;

            var inventory = inventoryProvider.Get(character.Id);
            var addedItem = inventory.AddItemInstance(instance, 1);
            //inventory.EquipBestItems();

            return addedItem.Id;
        }

        [Obsolete]
        public AddItemResult AddItem(SessionToken token, string userId, Guid itemId, out DataModels.InventoryItem itemStack)
        {
            itemStack = null;
            var character = GetCharacter(token, userId);
            if (character == null) return AddItemResult.Failed;
            return AddItemToCharacter(token, itemId, ref itemStack, character);
        }

        public AddItemResult AddItem(SessionToken token, Guid characterId, Guid itemId, out DataModels.InventoryItem itemStack)
        {
            itemStack = null;
            var character = GetCharacter(token, characterId);
            if (character == null) return AddItemResult.Failed;
            return AddItemToCharacter(token, itemId, ref itemStack, character);
        }

        private AddItemResult AddItemToCharacter(SessionToken token, Guid itemId, ref DataModels.InventoryItem itemStack, Character character)
        {
            var item = gameData.GetItem(itemId);
            if (item == null)
                return AddItemResult.Failed;


            if (!integrityChecker.VerifyPlayer(token.SessionId, character.Id, 0))
                return AddItemResult.Failed;

            var session = gameData.GetSession(token.SessionId);
            if (session == null)
                return AddItemResult.Failed;

            var sessionOwner = gameData.GetUser(session.UserId);
            if (sessionOwner == null || sessionOwner.Status >= 1)
                return AddItemResult.Failed;

            string tag = null;
            if (item.Category == (int)DataModels.ItemCategory.StreamerToken)
                tag = sessionOwner.UserId;

            var inventory = inventoryProvider.Get(character.Id);

            itemStack = inventory.AddItem(itemId, tag: tag, soulbound: item.Soulbound).FirstOrDefault();

            return AddItemResult.Added;
        }

        [Obsolete]
        public long SellItemInstanceToVendor(SessionToken sessionToken, string userId, Guid inventoryItemId, long amount)
        {
            var player = GetCharacter(sessionToken, userId);
            if (player == null) return 0;
            return SellItemToVendor(sessionToken, inventoryItemId, amount, player);
        }

        public long SellItemInstanceToVendor(SessionToken sessionToken, Guid characterId, Guid inventoryItemId, long amount)
        {
            var player = GetCharacter(sessionToken, characterId);
            if (player == null) return 0;
            return SellItemToVendor(sessionToken, inventoryItemId, amount, player);
        }

        private long SellItemToVendor(SessionToken sessionToken, Guid inventoryItemId, long amount, Character player)
        {
            if (!integrityChecker.VerifyPlayer(sessionToken.SessionId, player.Id, 0))
                return 0;

            //var targetItem = gameData.GetInventoryItem(item);
            var inventory = inventoryProvider.Get(player.Id);
            var itemToVendor = inventory.Get(inventoryItemId);
            if (itemToVendor.Item.Category == (int)DataModels.ItemCategory.StreamerToken)
            {
                return 0;
            }
            var resources = gameData.GetResources(player);
            if (resources == null) return 0;

            var session = gameData.GetSession(sessionToken.SessionId);
            if (amount <= itemToVendor.Amount)
            {
                var price = itemToVendor.Item.ShopSellPrice * amount;
                inventory.RemoveItem(itemToVendor, amount);
                resources.Coins += itemToVendor.Item.ShopSellPrice * amount;
                UpdateResources(session, player, resources);
                UpdateStockAndLogTransaction(player.Id, itemToVendor.ItemId, amount, price, false);
                return amount;
            }

            inventory.RemoveStack(itemToVendor);
            var totalPrice = itemToVendor.Amount * itemToVendor.Item.ShopSellPrice;
            resources.Coins += totalPrice;
            UpdateResources(session, player, resources);
            UpdateStockAndLogTransaction(player.Id, itemToVendor.ItemId, itemToVendor.Amount, totalPrice, false);
            return (int)itemToVendor.Amount;
        }

        public bool SellItemToVendor(Guid characterId, RavenNest.Models.InventoryItem item, long amount)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null) return false;
            var inventory = inventoryProvider.Get(character.Id);
            if (inventory.IsLocked(item.Id)) return false;
            var stack = gameData.GetInventoryItem(item.Id);
            if (stack == null) return false;


            var amountToVendor = Math.Min(amount, stack.Amount.GetValueOrDefault());
            if (amountToVendor <= 0) return false;

            var i = gameData.GetItem(item.ItemId);
            if (i == null || i.Category == (int)DataModels.ItemCategory.StreamerToken)
                return false;

            if (inventory.RemoveItem(stack, amountToVendor))
            {
                var resources = gameData.GetResources(character);
                if (resources == null) return false;
                var price = i.ShopSellPrice * amountToVendor;
                resources.Coins += price;

                UpdateStockAndLogTransaction(characterId, item.ItemId, amount, price, false);

                var sessionUserId = character.UserIdLock;
                if (sessionUserId != null)
                {
                    var session = gameData.GetSessionByUserId(sessionUserId.Value);
                    if (session != null)
                    {
                        UpdateResources(session, character, resources);
                        SendItemRemoveEvent(session, stack, amountToVendor, character);
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Logs a vendor transaction to the database. This is used for reporting.
        /// wasItemBought is either false (sold) or true (bought).
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="itemId"></param>
        /// <param name="amount"></param>
        /// <param name="totalPrice"></param>
        /// <param name="wasItemBought"></param>
        private void UpdateStockAndLogTransaction(Guid characterId, Guid itemId, long amount, double totalPrice, bool wasItemBought)
        {
            // Update Vendor Stock, we will not persist enchantment data for now, only itemId is relevant
            UpdateVendorItemStock(itemId, amount, wasItemBought);

            gameData.Add(
                new VendorTransaction
                {
                    Id = Guid.NewGuid(),
                    Amount = amount,
                    CharacterId = characterId,
                    ItemId = itemId,
                    PricePerItem = (long)(totalPrice / amount),
                    TotalPrice = (long)totalPrice,
                    TransactionType = wasItemBought,
                    Created = DateTime.UtcNow
                });
        }

        private void UpdateVendorItemStock(Guid itemId, long amount, bool wasItemBought)
        {
            var existingItem = gameData.GetVendorItemByItemId(itemId);
            if (existingItem == null)
            {
                existingItem = new VendorItem
                {
                    Id = Guid.NewGuid(),
                    ItemId = itemId,
                };

                gameData.Add(existingItem);
            }

            if (wasItemBought)
            {
                existingItem.Stock -= amount;
                if (existingItem.Stock <= 0)
                {
                    gameData.Remove(existingItem);
                }
            }
            else
            {
                existingItem.Stock += amount;
            }
        }

        public bool EquipItem(Guid characterId, RavenNest.Models.InventoryItem item)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null) return false;

            var inventoryItem = gameData.GetInventoryItem(item.Id);
            var inventory = inventoryProvider.Get(character.Id);
            if (inventory.EquipItem(inventoryItem))
            {
                SendItemEquipEvent(inventoryItem.Id, true, character);
                return true;
            }
            return false;
        }

        public bool UnequipItem(Guid characterId, RavenNest.Models.InventoryItem item)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null) return false;

            var inventoryItem = gameData.GetInventoryItem(item.Id);
            var inventory = inventoryProvider.Get(character.Id);
            if (inventory.UnequipItem(inventoryItem))
            {
                SendItemEquipEvent(inventoryItem.Id, false, character);
                return true;
            }
            return false;
        }

        public bool SendToCharacter(Guid characterId, RavenNest.Models.UserBankItem item, long amount)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null) return false;

            if (item.Amount < amount)
            {
                return false;
            }

            var bankItem = gameData.GetUserBankItem(item.Id);
            if (bankItem == null)
            {
                return false;
            }

            var inventory = inventoryProvider.Get(character.Id);
            var left = bankItem.Amount - amount;
            if (left == 0)
            {
                gameData.Remove(bankItem);
            }
            else
            {
                bankItem.Amount -= amount;
            }

            var newStack = inventory.AddItem(bankItem, amount);
            SendItemAddEvent(newStack, (int)amount, character);
            return true;
        }

        public bool SendToCharacter(Guid characterId, Guid otherCharacterId, RavenNest.Models.InventoryItem item, long amount)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null) return false;
            var otherCharacter = gameData.GetCharacter(otherCharacterId);
            if (otherCharacter == null) return false;
            // only send directly between your own characters. This is not a gift.
            if (otherCharacter.UserId != character.UserId) return false;

            var sourceInventory = inventoryProvider.Get(character.Id);
            var targetInventory = inventoryProvider.Get(otherCharacter.Id);
            var stack = gameData.GetInventoryItem(item.Id);

            if (sourceInventory.RemoveItem(stack, amount))
            {
                var newItemStack = targetInventory.AddItem(stack, amount);
                SendItemRemoveEvent(stack, (int)amount, character, true);
                SendItemAddEvent(newItemStack, (int)amount, otherCharacter);
                return true;
            }

            return false;
        }

        public long GiftItemInstance(SessionToken sessionToken, string gifterUserId, string receiverUserId, Guid itemId, long amount)
        {
            var gifter = GetCharacter(sessionToken, gifterUserId);
            if (gifter == null) return 0;
            if (!integrityChecker.VerifyPlayer(sessionToken.SessionId, gifter.Id, 0))
                return 0;
            var receiver = GetCharacter(sessionToken, receiverUserId);
            if (receiver == null) return 0;
            return GiftItem(itemId, amount, gifter, receiver);
        }

        public long GiftItemInstance(SessionToken sessionToken, Guid gifterUserId, Guid receiverUserId, Guid itemId, long amount)
        {
            var gifter = GetCharacter(sessionToken, gifterUserId);
            if (gifter == null || !integrityChecker.VerifyPlayer(sessionToken.SessionId, gifter.Id, 0)) return 0;
            var receiver = GetCharacter(sessionToken, receiverUserId);
            if (receiver == null) return 0;
            return GiftItem(itemId, amount, gifter, receiver);
        }

        private long GiftItem(Guid itemId, long amount, Character gifter, Character receiver)
        {
            var inventory = inventoryProvider.Get(gifter.Id);
            var item = inventory.Get(itemId);
            if (item.IsNull() || item.Soulbound) return 0;
            var gift = item;
            if (inventory.IsLocked(gift.Id)) return 0;
            var recvInventory = inventoryProvider.Get(receiver.Id);
            var amountToGift = gift.Amount >= amount ? amount : (int)gift.Amount;
            if (recvInventory.AddItem(gift, amountToGift) && inventory.RemoveItem(gift, amountToGift))
            {
                return amountToGift;
            }
            return 0;
        }

        public long GiftItem(
            SessionToken token,
            string gifterUserId,
            string receiverUserId,
            Guid itemId,
            long amount)
        {
            try
            {
                var gifter = GetCharacter(token, gifterUserId);
                if (gifter == null) return 0;

                if (!integrityChecker.VerifyPlayer(token.SessionId, gifter.Id, 0))
                    return 0;

                var receiver = GetCharacter(token, receiverUserId);
                if (receiver == null) return 0;

                var item = gameData.GetItem(itemId);
                if (item == null || item.Soulbound)
                    return 0;

                var session = gameData.GetSession(token.SessionId);
                var sessionOwner = gameData.GetUser(session.UserId);

                string itemTag = null;
                if (item.Category == (int)DataModels.ItemCategory.StreamerToken)
                    itemTag = sessionOwner.UserId;

                var inventory = inventoryProvider.Get(gifter.Id);
                var gift = inventory.GetUnequippedItem(itemId, tag: itemTag);

                if (inventory.IsLocked(gift.Id)) return 0;

                if (gift.IsNull() || gift.Amount == 0)
                    return 0;

                if (gift.Soulbound)
                    return 0;

                var recvInventory = inventoryProvider.Get(receiver.Id);
                var amountToGift = gift.Amount >= amount ? amount : (int)gift.Amount;
                if (recvInventory.AddItem(gift, amountToGift) && inventory.RemoveItem(gift, amountToGift))
                {
                    return amountToGift;
                }

                return 0;
            }
            catch (Exception exc)
            {
                logger.LogError($"Unable to gift item (g {gifterUserId}, r {receiverUserId}, i {itemId}, a x{amount}): " + exc);
                return 0;
            }
        }


        public bool SendToStash(Guid characterId, RavenNest.Models.InventoryItem invItemInput, long amount)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null) return false;
            var inventory = inventoryProvider.Get(character.Id);
            if (inventory.IsLocked(invItemInput.Id)) return false;
            var stack = gameData.GetInventoryItem(invItemInput.Id);
            if (stack == null) return false;
            if (inventory.RemoveItem(stack, amount))
            {
                // Check whether or not this item can be stacked.
                //  if item can be stacked, increment existing bank item if one exists.
                //  otherwise create a new one.

                try
                {
                    var canBeStacked = PlayerInventory.CanBeStacked(stack);
                    if (canBeStacked)
                    {
                        var bankItems = gameData.GetUserBankItems(character.UserId);
                        var existing = bankItems.FirstOrDefault(x => PlayerInventory.CanBeStacked(x, stack));
                        if (existing != null)
                        {
                            existing.Amount += amount;
                            return true;
                        }
                    }

                    gameData.Add(CreateBankItem(character, stack, amount));
                    return true;
                }
                finally
                {
                    SendItemRemoveEvent(stack, amount, character, true);
                }
            }

            return false;
        }

        private static DataModels.UserBankItem CreateBankItem(Character character, DataModels.InventoryItem item, long amount)
        {
            return new DataModels.UserBankItem
            {
                Id = Guid.NewGuid(),
                Amount = amount,
                Enchantment = item.Enchantment,
                Flags = item.Flags ?? 0,
                ItemId = item.ItemId,
                Name = item.Name,
                Soulbound = item.Soulbound.GetValueOrDefault(),
                Tag = item.Tag,
                TransmogrificationId = item.TransmogrificationId,
                UserId = character.UserId
            };
        }

        [Obsolete]
        public bool EquipItemInstance(SessionToken token, string userId, Guid inventoryItemId)
        {
            var character = GetCharacter(token, userId);
            if (character == null) return false;
            var inventory = inventoryProvider.Get(character.Id);
            var invItem = inventory.Get(inventoryItemId);
            var skills = gameData.GetCharacterSkills(character.SkillsId);
            if (invItem.IsNull() || !PlayerInventory.CanEquipItem(gameData.GetItem(invItem.ItemId), skills))
                return false;
            return inventory.EquipItem(invItem);
        }

        public bool EquipItemInstance(SessionToken token, Guid characterId, Guid inventoryItemId)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return false;
            var inventory = inventoryProvider.Get(character.Id);
            var invItem = inventory.Get(inventoryItemId);
            var skills = gameData.GetCharacterSkills(character.SkillsId);
            if (invItem.IsNull() || !PlayerInventory.CanEquipItem(gameData.GetItem(invItem.ItemId), skills))
                return false;
            return inventory.EquipItem(invItem);
        }

        public bool EquipItem(SessionToken token, Guid characterId, Guid itemId)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return false;
            var item = gameData.GetItem(itemId);
            if (item == null) return false;
            var inventory = inventoryProvider.Get(character.Id);
            var invItem = inventory.GetUnequippedItem(itemId);
            var skills = gameData.GetCharacterSkills(character.SkillsId);
            if (invItem.IsNull() || !PlayerInventory.CanEquipItem(gameData.GetItem(invItem.ItemId), skills))
                return false;
            return inventory.EquipItem(invItem);
        }


        [Obsolete]
        public bool EquipItem(SessionToken token, string userId, Guid itemId)
        {
            var character = GetCharacter(token, userId);
            if (character == null) return false;
            var item = gameData.GetItem(itemId);
            if (item == null) return false;
            var inventory = inventoryProvider.Get(character.Id);
            var invItem = inventory.GetUnequippedItem(itemId);
            var skills = gameData.GetCharacterSkills(character.SkillsId);
            if (invItem.IsNull() || !PlayerInventory.CanEquipItem(gameData.GetItem(invItem.ItemId), skills))
                return false;
            return inventory.EquipItem(invItem);
        }

        [Obsolete]
        public bool UnequipItemInstance(SessionToken token, string userId, Guid inventoryItemId)
        {
            var character = GetCharacter(token, userId);
            if (character == null) return false;
            var inventory = inventoryProvider.Get(character.Id);
            var invItem = inventory.Get(inventoryItemId);
            if (invItem.IsNull()) return false;
            return inventory.UnequipItem(invItem);
        }

        [Obsolete]
        public bool UnequipItem(SessionToken token, string userId, Guid itemId)
        {
            var character = GetCharacter(token, userId);
            if (character == null) return false;

            var inventory = inventoryProvider.Get(character.Id);
            var invItem = inventory.GetEquippedItem(itemId);
            if (invItem.IsNull()) return false;

            return inventory.UnequipItem(invItem);
        }
        public bool UnequipItemInstance(SessionToken token, Guid characterId, Guid inventoryItemId)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return false;
            var inventory = inventoryProvider.Get(characterId);
            var invItem = inventory.Get(inventoryItemId);
            if (invItem.IsNull()) return false;
            return inventory.UnequipItem(invItem);
        }

        public bool UnequipItem(SessionToken token, Guid characterId, Guid itemId)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return false;
            var inventory = inventoryProvider.Get(character.Id);
            var invItem = inventory.GetEquippedItem(itemId);
            if (invItem.IsNull()) return false;
            return inventory.UnequipItem(invItem);
        }

        [Obsolete]
        public bool EquipBestItems(SessionToken token, string userId)
        {
            var character = GetCharacter(token, userId);
            if (character == null) return false;
            var inventory = inventoryProvider.Get(character.Id);
            inventory.EquipBestItems();
            return true;
        }
        public bool EquipBestItems(SessionToken token, Guid characterId)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return false;
            var inventory = inventoryProvider.Get(character.Id);
            inventory.EquipBestItems();
            return true;
        }

        [Obsolete]
        public bool UnequipAllItems(SessionToken token, string userId)
        {
            var character = GetCharacter(token, userId);
            if (character == null) return false;
            var inventory = inventoryProvider.Get(character.Id);
            inventory.UnequipAllItems();
            return true;
        }

        public bool UnequipAllItems(SessionToken token, Guid characterId)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return false;
            var inventory = inventoryProvider.Get(character.Id);
            inventory.UnequipAllItems();
            return true;
        }

        [Obsolete]
        public bool ToggleHelmet(SessionToken token, string userId)
        {
            var character = GetCharacter(token, userId);
            if (character == null) return false;

            var appearance = gameData.GetAppearance(character.SyntyAppearanceId);
            if (appearance == null) return false;

            appearance.HelmetVisible = !appearance.HelmetVisible;
            return true;
        }

        public bool ToggleHelmet(SessionToken token, Guid characterId)
        {
            var character = GetCharacter(token, characterId);
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

        public IReadOnlyList<WebsiteAdminPlayer> GetWebsiteAdminPlayers()
        {
            var chars = gameData.GetCharacters();
            var result = chars.Select(x => new
            {
                User = gameData.GetUser(x.UserId),
                Character = x
            })
            .SelectWhere(x => x.Character != null && x.User != null, x => x.User.MapForAdmin(gameData, x.Character));

            return result;
        }
        public IReadOnlyList<WebsiteAdminPlayer> GetAdminWebsitePlayers(Guid userId)
        {
            var user = gameData.GetUser(userId);
            var chars = gameData.GetCharactersByUserId(user.Id);
            return chars.Select(x => new
            {
                User = user,
                Character = x
            })
            .SelectWhere(x => x.Character != null && x.User != null, x => x.User.MapForAdmin(gameData, x.Character));
        }

        public IReadOnlyList<Player> GetPlayerWithoutAdmins()
        {
            return GetPlayers((user, character) => user != null && (user.Status == null || user.Status == 0) && !user.IsModerator.GetValueOrDefault() && !user.IsAdmin.GetValueOrDefault());
        }

        public IReadOnlyDictionary<Guid, HighscorePlayer> GetHighscorePlayers()
        {
            // NOTE: we should make a GetPlayers that only includes skills, so we don't have to load all items, etc.
            //       this should speed up things tremendously.

            return GetPlayersForHighscore(-1);
        }

        public IReadOnlyDictionary<System.Guid, HighscorePlayer> GetHighscorePlayers(int characterIndex)
        {
            // NOTE: we should make a GetPlayers that only includes skills, so we don't have to load all items, etc.
            //       this should speed up things tremendously.

            return GetPlayersForHighscore(characterIndex);
        }

        public IReadOnlyList<Player> GetPlayers()
        {
            return GetPlayers((user, character) => user != null && (user.Status == null || user.Status == 0));
        }

        private IReadOnlyList<Player> GetPlayers(Func<User, Character, bool> predicate)
        {
            var chars = gameData.GetCharacters();
            var result = new List<Player>();
            foreach (var c in chars)
            {
                if (c == null)
                {
                    continue;
                }

                var user = gameData.GetUser(c.UserId);
                if (predicate(user, c))
                {
                    result.Add(user.Map(gameData, c));
                }
            }
            return result;
        }

        private IReadOnlyDictionary<Guid, HighscorePlayer> GetPlayersForHighscore(int characterIndex)
        {
            var chars = gameData.GetCharacters();
            var result = new Dictionary<Guid, HighscorePlayer>();
            foreach (var c in chars)
            {
                if (c == null || (characterIndex >= 0 && c.CharacterIndex != characterIndex))
                {
                    continue;
                }

                var hsp = c.MapForHighscore(gameData);
                if (hsp != null)
                {
                    result[c.Id] = hsp;
                }
            }
            return result;
        }

        public bool UpdateStatistics(SessionToken token, string userId, double[] statistics, Guid? characterId = null)
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

        public bool UpdateAppearance(Guid characterId, RavenNest.Models.SyntyAppearance appearance)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null)
                return false;

            var user = gameData.GetUser(character.UserId);
            if (user == null)
                return false;

            UpdateCharacterAppearance(appearance, character);

            var sessionOwnerUserId = character.UserIdLock.GetValueOrDefault();
            var gameSession = gameData.GetSessionByUserId(sessionOwnerUserId);

            if (gameSession != null)
            {
                var gameEvent = gameData.CreateSessionEvent(GameEventType.PlayerAppearance,
                    gameSession,
                    new SyntyAppearanceUpdate
                    {
                        PlayerId = character.Id,
                        Value = appearance
                    });

                gameData.EnqueueGameEvent(gameEvent);
            }

            return true;
        }

        public bool UpdateAppearance(string userId, string identifier, RavenNest.Models.SyntyAppearance appearance)
        {
            try
            {
                var user = gameData.GetUserByTwitchId(userId);
                if (user == null) return false;

                var character = gameData.GetCharacterByUserId(user.Id, identifier);
                if (character == null) return false;

                UpdateCharacterAppearance(appearance, character);

                var sessionOwnerUserId = character.UserIdLock.GetValueOrDefault();
                var gameSession = gameData.GetSessionByUserId(sessionOwnerUserId);

                if (gameSession != null)
                {
                    var gameEvent = gameData.CreateSessionEvent(GameEventType.PlayerAppearance, gameSession, new SyntyAppearanceUpdate
                    {
                        PlayerId = character.Id,
                        Value = appearance
                    });

                    gameData.EnqueueGameEvent(gameEvent);
                }

                return true;
            }
            catch (Exception exc)
            {
                logger.LogError("Exception updating appearance: " + exc.ToString());
                return false;
            }
        }

        private Player GetPlayerByUser(User user, string identifier)
        {
            var character = gameData.GetCharacterByUserId(user.Id, identifier);
            if (character != null)
                return character.Map(gameData, user);

            return null;
        }

        private WebsitePlayer GetWebsitePlayer(User user, string identifier)
        {
            if (user == null) return null;
            var character = gameData.GetCharacterByUserId(user.Id, identifier);
            return GetWebsitePlayer(user, character);
        }

        public WebsitePlayer GetWebsitePlayer(User user, Character character)
        {
            if (character == null) return new WebsitePlayer
            {
                Appearance = new RavenNest.Models.SyntyAppearance(),
                Clan = new RavenNest.Models.Clan(),
                InventoryItems = new List<RavenNest.Models.InventoryItem>(),
                Skills = new SkillsExtended(),
                Resources = new RavenNest.Models.Resources(),
                State = new RavenNest.Models.CharacterState(),
                Statistics = new RavenNest.Models.Statistics()
            };

            return character.MapForWebsite(gameData, user);
        }

        private void UpdateCharacterAppearance(RavenNest.Models.SyntyAppearance appearance, Character character)
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

        internal void SaveExperience(SessionToken sessionToken, SaveExperienceRequest saveExp)
        {
            if (sessionToken == null || saveExp == null)
            {
                return;
            }

            var gameSession = gameData.GetSession(sessionToken.SessionId);
            if (gameSession == null)
            {
                logger.LogError("Save Exp Request received from an invalid game session. " + sessionToken.UserName);
                return;
            }

            var sessionState = gameData.GetSessionState(gameSession.Id);
            if (sessionState != null)
            {
                sessionState.LastExpRequest = DateTime.UtcNow;
            }

            if (GameVersion.IsLessThanOrEquals(sessionState.ClientVersion, GameUpdates.DisableExpSave_LessThanOrEquals))
            {
                var hasBeenReported = sessionState.GetOrDefault<bool>(GameUpdates.DisableExpSave_LessThanOrEquals);
                if (!hasBeenReported)
                {
                    // to avoid spamming, lets only log this once per session.
                    sessionState[GameUpdates.DisableExpSave_LessThanOrEquals] = true;
                    logger.LogError("Save Exp Request received an old game client. Session=" + sessionToken.UserName + ", Version=" + sessionState.ClientVersion);
                }
                return;
            }

            var sessionOwner = gameData.GetUser(gameSession.UserId);
            if (sessionOwner.Status >= 1)
            {
                logger.LogError("The user session from " + sessionOwner.UserName + " trying to save players, but the owner has been banned.");
                return;
            }

            foreach (var data in saveExp.ExpUpdates)
            {
                var character = gameData.GetCharacter(data.CharacterId);
                if (character == null)
                {
                    logger.LogError("Trying to update a character that does not exist. ID: " + data.CharacterId);
                    continue;
                }

                // lets take ownership of thie character.
                if (character.UserIdLock == null)
                {
                    character.UserIdLock = sessionOwner.Id;
                    logger.LogWarning("Session Ownership changed! Trying to update a character without session owner. Current Session: " + sessionOwner.UserName + " Character: " + character.Name);
                }

                if (character.UserIdLock != sessionOwner.Id)
                {
                    var characterSessionOwner = character.UserIdLock != null ? gameData.GetUser(character.UserIdLock.GetValueOrDefault()) : null;
                    var partOfSession = characterSessionOwner != null ? characterSessionOwner.UserName : "";

                    logger.LogError("Trying to update a character that does not belong to the session owner. Current Session: " + sessionOwner.UserName + " Character: " + character.Name + ", UserIdLock: " + partOfSession);
                    // send remove from this session.
                    SendRemovePlayerFromSession(character, gameSession, "Character is part of another session.");
                    continue;
                }

                var characterSessionState = gameData.GetCharacterSessionState(sessionToken.SessionId, character.Id);
                characterSessionState.LastExpSaveRequest = DateTime.UtcNow;

                if (characterSessionState.Compromised)
                {
                    continue;
                }

                var skills = gameData.GetCharacterSkills(character.SkillsId);
                if (skills == null)
                {
                    skills = gameData.GenerateSkills();
                    character.SkillsId = skills.Id;
                    gameData.Add(skills);
                }

                var user = gameData.GetUser(character.UserId);
                foreach (var update in data.Skills)
                {
                    if (update.Level > GameMath.MaxLevel)
                    {
                        continue;
                    }

                    var skill = skills.GetSkill(update.Index);
                    if (skill == null) continue;

                    var level = update.Level;
                    var experience = update.Experience;
                    var timeSinceLastSkillUpdate = DateTime.UtcNow - characterSessionState.LastExpUpdate;
                    var existingLevel = skill.Level;

                    if (skill.Level == GameMath.MaxLevel)
                    {
                        var maxExp = GameMath.ExperienceForLevel(GameMath.MaxLevel + 1);
                        if (experience >= maxExp)
                        {
                            experience = maxExp;
                        }
                    }

                    if (!user.IsAdmin.GetValueOrDefault() && user.IsModerator.GetValueOrDefault())
                    {
                        if (level > 100 && existingLevel < level * 0.5)
                        {
                            if (timeSinceLastSkillUpdate <= TimeSpan.FromSeconds(10))
                            {
                                logger.LogError("The user " + sessionOwner.UserName + " has been banned for cheating. Character: " + character.Name + " (" + character.Id + "). Reason: Level changed from " + existingLevel + " to " + level);
                                BanUserAndCloseSession(gameSession, characterSessionState, sessionOwner);
                                break;
                            }
                        }
                    }

                    skills.Set(skill.Index, level, experience);

                    if (existingLevel != level)
                    {
                        UpdateCharacterSkillRecord(character.Id, skill.Index, level, experience);
                    }

                    characterSessionState.LastExpUpdate = DateTime.UtcNow;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    TrySendToExtensionAsync(character, new CharacterExpUpdate
                    {
                        CharacterId = character.Id,
                        Experience = experience,
                        Level = level,
                        SkillIndex = skill.Index,
                        Percent = SkillsExtended.GetPercentForNextLevel(skill.Level, skill.Experience),
                    });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
            }
        }

        internal void SaveState(SessionToken sessionToken, SaveStateRequest stateUpdate)
        {
            if (sessionToken == null || stateUpdate == null)
            {
                return;
            }

            var gameSession = gameData.GetSession(sessionToken.SessionId);
            if (gameSession == null)
            {
                logger.LogError("Save State Request received from an invalid game session. " + sessionToken.UserName);
                return;
            }

            var sessionState = gameData.GetSessionState(gameSession.Id);
            if (sessionState != null)
            {
                sessionState.LastStateRequest = DateTime.UtcNow;
            }

            var sessionOwner = gameData.GetUser(gameSession.UserId);
            if (sessionOwner.Status >= 1)
            {
                logger.LogError("The user session from " + sessionOwner.UserName + " trying to save players, but the owner has been banned.");
                return;
            }

            foreach (var data in stateUpdate.StateUpdates)
            {
                DataModels.CharacterState characterState = null;
                var character = gameData.GetCharacter(data.CharacterId);
                if (character == null)
                {
                    logger.LogError("Trying to update a character that does not exist. ID: " + data.CharacterId);
                    continue;
                }

                // lets take ownership of thie character.
                if (character.UserIdLock == null)
                {
                    character.UserIdLock = sessionOwner.Id;
                    logger.LogWarning("Session Ownership changed! Trying to update a character without session owner. Current Session: " + sessionOwner.UserName + " Character: " + character.Name);
                }

                if (character.UserIdLock != sessionOwner.Id)
                {
                    var characterSessionOwner = character.UserIdLock != null ? gameData.GetUser(character.UserIdLock.GetValueOrDefault()) : null;
                    var partOfSession = characterSessionOwner != null ? characterSessionOwner.UserName : "";

                    logger.LogError("Trying to update a character that does not belong to the session owner. Current Session: " + sessionOwner.UserName + " Character: " + character.Name + ", UserIdLock: " + partOfSession);
                    // send remove from this session.
                    SendRemovePlayerFromSession(character, gameSession, "Character is part of another session.");
                    continue;
                }

                var characterSessionState = gameData.GetCharacterSessionState(sessionToken.SessionId, character.Id);
                characterSessionState.LastStateSaveRequest = DateTime.UtcNow;
                characterSessionState.LastStateUpdate = DateTime.UtcNow;


                if (character.StateId != null)
                {
                    characterState = gameData.GetCharacterState(character.StateId);
                }

                if (characterState == null)
                {
                    var state = CreateCharacterState(data);
                    gameData.Add(state);
                    character.StateId = state.Id;
                }
                else
                {
                    var state = gameData.GetCharacterState(character.StateId);
                    SetCharacterState(state, data);
                }


#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                TrySendToExtensionAsync(character, data);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            }
        }

        /// <summary>
        /// New character update api using tcp api.
        /// </summary>
        /// <param name=""></param>
        /// <returns></returns>
        [Obsolete]
        public bool UpdateCharacter(SessionToken token, CharacterUpdate data)
        {
            // seem to happen on server restarts.
            if (token == null || data == null || data.CharacterId == Guid.Empty)
            {
                return false;
            }

            var gameSession = gameData.GetSession(token.SessionId);
            var character = gameData.GetCharacter(data.CharacterId);

            if (character == null)
            {
                throw new Exception("Unable to update character with ID " + data.CharacterId + ". No such character could be found.");
            }

            if (gameSession == null)
            {
                logger.LogError("Unable to update character with ID " + data.CharacterId + " (" + character.Name + "). Character is not part of a game session.");
                return false;
            }

            if (character.UserIdLock == null || !AcquiredUserLock(token, character))
            {
                SendRemovePlayerFromSession(character, gameSession, "[TCP API->UpdateCharacter]");
                return true;
            }

            //var sessionState = gameData.GetSessionState(gameSession.Id);
            var characterSessionState = gameData.GetCharacterSessionState(token.SessionId, character.Id);
            if (characterSessionState.Compromised)
            {
                logger.LogError("Trying to update an out of sync player: " + character.Name + " (" + character.Id + ")");
                return true;
            }

            var sessionOwner = gameData.GetUser(gameSession.UserId);
            if (sessionOwner.Status >= 1)
            {
                logger.LogError("The user session from " + sessionOwner.UserName + " trying to save players, but the owner has been banned.");
                return true;
            }

            var sessionState = gameData.GetSessionState(gameSession.Id);
            if (sessionState != null)
            {
                sessionState.LastExpRequest = DateTime.UtcNow;
            }


            if (GameVersion.IsLessThanOrEquals(sessionState.ClientVersion, GameUpdates.DisableExpSave_LessThanOrEquals))
            {
                var hasBeenReported = sessionState.GetOrDefault<bool>(GameUpdates.DisableExpSave_LessThanOrEquals);
                // to avoid spamming, lets only log this once per session.
                if (!hasBeenReported)
                {
                    sessionState[GameUpdates.DisableExpSave_LessThanOrEquals] = true;
                    logger.LogError("Save Exp Request received an old game client. Session=" + token.UserName + ", Version=" + sessionState.ClientVersion);
                }
                return false;
            }

            /*
                Update Character State
             */
            DataModels.CharacterState characterState = null;
            if (character.StateId != null)
            {
                characterState = gameData.GetCharacterState(character.StateId);
            }

            if (characterState == null)
            {
                var state = CreateCharacterState(data);
                gameData.Add(state);
                character.StateId = state.Id;
            }
            else
            {
                var state = gameData.GetCharacterState(character.StateId);
                SetCharacterState(state, data);
            }

            /*
                Update Character Experience
             */

            var skills = gameData.GetCharacterSkills(character.SkillsId);

            if (skills == null)
            {
                skills = gameData.GenerateSkills();
                character.SkillsId = skills.Id;
                gameData.Add(skills);
            }


            var user = gameData.GetUser(character.UserId);


            if (data.Skills != null && data.Skills.Length > 0)
            {
                characterSessionState.LastExpSaveRequest = DateTime.UtcNow;
            }

            foreach (var update in data.Skills)
            {
                if (update.Level > GameMath.MaxLevel)
                {
                    continue;
                }

                var skill = skills.GetSkill(update.Index);
                if (skill == null) continue;

                var level = update.Level;
                var experience = update.Experience;
                var timeSinceLastSkillUpdate = DateTime.UtcNow - characterSessionState.LastExpUpdate;
                var existingLevel = skill.Level;


                if (!user.IsAdmin.GetValueOrDefault() && user.IsModerator.GetValueOrDefault())
                {
                    if (level > 100 && existingLevel < level * 0.5)
                    {
                        if (timeSinceLastSkillUpdate <= TimeSpan.FromSeconds(10))
                        {
                            logger.LogError("The user " + sessionOwner.UserName + " has been banned for cheating. Character: " + character.Name + " (" + character.Id + "). Reason: Level changed from " + existingLevel + " to " + level);
                            BanUserAndCloseSession(gameSession, characterSessionState, sessionOwner);
                            return true;
                        }
                    }
                }

                skills.Set(skill.Index, level, experience);

                if (existingLevel != level)
                {
                    UpdateCharacterSkillRecord(character.Id, skill.Index, level, experience);
                }

                characterSessionState.LastExpUpdate = DateTime.UtcNow;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                TrySendToExtensionAsync(character, new CharacterExpUpdate
                {
                    CharacterId = character.Id,
                    Experience = experience,
                    Level = level,
                    SkillIndex = skill.Index,
                    Percent = SkillsExtended.GetPercentForNextLevel(skill.Level, skill.Experience),
                });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }

            return true;
        }


        public bool UpdateExperience(SessionToken token, int skillIndex, int level, double experience, Guid characterId)
        {
            try
            {
                var gameSession = gameData.GetSession(token.SessionId);
                var character = gameData.GetCharacter(characterId);
                if (character == null)
                    throw new Exception("Unable to save exp. Character with ID " + characterId + " could not be found.");

                var sessionState = gameData.GetSessionState(gameSession.Id);
                var characterSessionState = gameData.GetCharacterSessionState(token.SessionId, character.Id);
                if (characterSessionState.Compromised)
                {
                    logger.LogError("Trying to update an out of sync player: " + character.Name + " (" + character.Id + ")");
                    return true;
                }

                var sessionOwner = gameData.GetUser(gameSession.UserId);
                if (sessionOwner.Status >= 1)
                {
                    logger.LogError("The user session from " + sessionOwner.UserName + " trying to save players, but the owner has been banned.");
                    return true;
                }

                // Temporary solution to skill rollback, block saving skills from clients other than the one definied.
                if (!sessionOwner.IsAdmin.GetValueOrDefault() && !gameData.IsExpectedVersion(gameSession))
                {
                    return true;
                }

                if (GameVersion.IsLessThanOrEquals(sessionState.ClientVersion, GameUpdates.DisableExpSave_LessThanOrEquals))
                {
                    var hasBeenReported = sessionState.GetOrDefault<bool>(GameUpdates.DisableExpSave_LessThanOrEquals);
                    if (!hasBeenReported)
                    {
                        // to avoid spamming, lets only log this once per session.
                        sessionState[GameUpdates.DisableExpSave_LessThanOrEquals] = true;
                        logger.LogError("Save Exp Request received an old game client. Session=" + token.UserName + ", Version=" + sessionState.ClientVersion);
                    }
                    return true;
                }

                var removeFromSession = !AcquiredUserLock(token, character) && character.UserIdLock != null;
                var skills = gameData.GetCharacterSkills(character.SkillsId);

                if (skills == null)
                {
                    skills = gameData.GenerateSkills();
                    character.SkillsId = skills.Id;
                    gameData.Add(skills);
                }

                /*
                    Temporary Debugging
                 */
                if (removeFromSession)
                {
                    SendRemovePlayerFromSession(character, gameSession, "[Update Training Skill]");
                    //if (character.UserIdLock != null)
                    //{
                    //    var activeSessionOwner = gameData.GetUser(character.UserIdLock.GetValueOrDefault());
                    //    if (activeSessionOwner != null)
                    //    {
                    //        logger.LogError($"{character.Name} tried to save from a session it was not apart of. Session owner: {sessionOwner.UserName}, but character is part of {activeSessionOwner.UserName}.");
                    //    }
                    //}
                    //else
                    //{
                    //    logger.LogError($"{character.Name} tried to save from a session it was not apart of. Session owner: {sessionOwner.UserName}, but character is not in any session.");
                    //}
                    return true;
                }

                var gains = characterSessionState.ExpGain;
                characterSessionState.LastExpSaveRequest = DateTime.UtcNow;
                var user = gameData.GetUser(character.UserId);
                var updated = false;
                var timeSinceLastSkillUpdate = DateTime.UtcNow - characterSessionState.LastExpUpdate;
                var existingLevel = skills.GetLevel(skillIndex);

                if (level > GameMath.MaxLevel)
                {
                    return true;
                }

                if (!user.IsAdmin.GetValueOrDefault() && user.IsModerator.GetValueOrDefault())
                {
                    if (level > 100 && existingLevel < level * 0.5)
                    {
                        if (timeSinceLastSkillUpdate <= TimeSpan.FromSeconds(10))
                        {
                            logger.LogError("The user " + sessionOwner.UserName + " has been banned for cheating. Character: " + character.Name + " (" + character.Id + "). Reason: Level changed from " + existingLevel + " to " + level);
                            BanUserAndCloseSession(gameSession, characterSessionState, sessionOwner);
                            return true;
                        }
                    }
                }

                if (existingLevel != level)
                {
                    UpdateCharacterSkillRecord(character.Id, skillIndex, level, experience);
                }

                skills.Set(skillIndex, level, experience);
                updated = true;

                if (updated)
                {
                    characterSessionState.LastExpUpdate = DateTime.UtcNow;
                    TrySendToExtensionAsync(character, new CharacterExpUpdate
                    {
                        CharacterId = character.Id,
                        Experience = experience,
                        Level = level,
                        SkillIndex = skillIndex,
                        Percent = SkillsExtended.GetPercentForNextLevel(level, experience),
                    });
                }

                return true;
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                return false;
            }
        }


        public async Task<bool> UnstuckPlayerAsync(Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null)
            {
                return false;
            }

            await SendRejoinAsync(character, "Unstuck used by admin.", async () =>
            {
                var state = gameData.GetCharacterState(character.StateId);
                if (state == null)
                    return;

                if (state.Island == null)
                    return;

                switch (state.Island)
                {
                    case "Home":
                        state.X = 119.6936f;
                        state.Y = -2.073665f;
                        state.Z = 62.14654f;
                        break;

                    case "Away":
                        state.X = 223.13f;
                        state.Y = -2.324998f;
                        state.Z = -263.5f;
                        break;

                    case "Kyo":
                        state.X = -364.4747f;
                        state.Y = -1.838f;
                        state.Z = 75.62f;
                        break;

                    case "Ironhill":
                        state.X = 29.53798f;
                        state.Y = -1.868665f;
                        state.Z = 294.4587f;
                        break;

                    case "Heim":
                        state.X = -285.842f;
                        state.Y = -1.918666f;
                        state.Z = -398.4614f;
                        break;

                    default:
                        state.Y = state.Y + 5f;
                        break;
                }
            });

            return true;
        }

        public async Task<bool> CloneSkillsAndStateAsync(Guid fromCharacterId, Guid toCharacterId)
        {
            var charTo = gameData.GetCharacter(toCharacterId);
            var charFrom = gameData.GetCharacter(fromCharacterId);
            if (charFrom == null || charTo == null)
            {
                return false;
            }

            var skillsFrom = gameData.GetCharacterSkills(charFrom.SkillsId);
            var skillsTo = gameData.GetCharacterSkills(charTo.SkillsId);
            if (skillsFrom == null || skillsTo == null)
            {
                return false;
            }

            await SendRejoinAsync(charTo, "Stats has been modified by admin.", async () =>
            {

                /*
                    We will also clone state.
                 */

                var fromState = gameData.GetCharacterState(charFrom.StateId);
                var toState = gameData.GetCharacterState(charTo.StateId);
                if (fromState != null && toState != null)
                {
                    toState.Task = fromState.TaskArgument;
                    toState.Task = fromState.Task;
                    toState.RestedTime = fromState.RestedTime;
                    toState.Island = fromState.Island;
                    toState.InOnsen = fromState.InOnsen;
                    toState.JoinedDungeon = fromState.JoinedDungeon;
                    toState.EstimatedTimeForLevelUp = fromState.EstimatedTimeForLevelUp;
                    toState.ExpPerHour = fromState.ExpPerHour;
                    toState.X = fromState.X;
                    toState.Y = fromState.Y;
                    toState.Z = fromState.Z;
                    toState.Health = fromState.Health;
                }

                foreach (var s0 in skillsFrom.GetSkills())
                {
                    var s1 = skillsTo.GetSkill(s0.Name);
                    s1.Experience = s0.Experience;
                    s1.Level = s0.Level;
                }
            });

            return true;
        }

        public async Task<bool> ResetPlayerSkillsAsync(Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null)
            {
                return false;
            }

            string reason = "Stats has been reset by admin";

            await SendRejoinAsync(character, reason, async () =>
            {
                var characterSkills = gameData.GetCharacterSkills(character.SkillsId);
                if (characterSkills == null)
                {
                    return;
                }

                var state = gameData.GetCharacterState(character.StateId);
                if (state != null)
                {
                    state.Health = 10;
                }

                foreach (var s in characterSkills.GetSkills())
                {
                    if (s.Name == nameof(Skills.Health))
                    {
                        s.Experience = 1000;
                        s.Level = 10;
                    }
                    else
                    {
                        s.Level = 1;
                        s.Experience = 0;
                    }
                }

            });

            return true;
        }


        public async Task<bool> UpdatePlayerSkillAsync(Guid characterId, string skillName, int level, float levelProgress)
        {
            if (levelProgress > 1)
            {
                levelProgress /= 100f;
            }

            if (level <= 0 || level > GameMath.MaxLevel)
            {
                return false;
            }

            var character = gameData.GetCharacter(characterId);
            if (character == null)
            {
                return false;
            }
            var characterSkills = gameData.GetCharacterSkills(character.SkillsId);
            if (characterSkills == null)
            {
                return false;
            }

            await SendRejoinAsync(character, "Experience has been modified by admin", async () =>
            {
                var skill = characterSkills.GetSkill(skillName);
                if (skill == null)
                {
                    return;
                }

                skill.Level = level;
                skill.Experience = levelProgress * GameMath.ExperienceForLevel(level + 1);

                await TrySendToExtensionAsync(character, new CharacterExpUpdate
                {
                    CharacterId = character.Id,
                    SkillIndex = skill.Index,
                    Experience = skill.Experience,
                    Level = level,
                    Percent = SkillsExtended.GetPercentForNextLevel(skill.Level, skill.Experience),
                });
            });


            // Right now: 
            //  1. Lock future updates untill player leave/join a game.
            //  2. Update any extension with the new stats
            //  3. Kick player from any active session.

            // TODO:
            // 1. get which session this character is in
            // 2. get the character session state
            // 3. update the "requested" level and exp for the particular skill
            // 4. send the "level change" to the game sessions
            // 5. send the new level to the extension connections
            // After the #4; stop accepting new exp changes to that skill until the client has sent an update with similar level and exp
            // if the save still tries to save a higher exp. Then kick the player from their game session and add them back. Repeat until OK

            return true;
        }

        private async Task SendRejoinAsync(Character character, string reason, Func<Task> onLockAsync = null)
        {
            Guid characterId = character.Id;
            var gameSession = gameData.GetSessionByCharacterId(characterId);
            if (gameSession != null)
            {
                //SendRemovePlayerFromSession(character, gameSession);
                var characterSessionState = gameData.GetCharacterSessionState(gameSession.Id, character.Id);
                if (characterSessionState != null)
                {
                    characterSessionState.Compromised = true;
                    characterSessionState.LastExpUpdate = DateTime.UtcNow;
                    characterSessionState.LastExpSaveRequest = DateTime.UtcNow;
                }
            }

            if (onLockAsync != null)
            {
                await onLockAsync();
            }

            if (gameSession != null)
            {
                // Try and remove the player from the session and then add them back
                if (await RemovePlayerFromActiveSession(gameSession, characterId))
                {
                    SendRemovePlayerFromSession(character, gameSession, reason);
                    await Task.Delay(100);
                    SendPlayerAddToSession(character, gameSession);
                }
            }
        }

        private void UpdateCharacterSkillRecord(Guid characterId, int skillIndex, int skillLevel, double skillExp)
        {
            CharacterSkillRecord skillRecord = gameData.GetCharacterSkillRecord(characterId, skillIndex);
            if (skillRecord == null)
            {
                gameData.Add(new CharacterSkillRecord
                {
                    DateReached = DateTime.UtcNow,
                    CharacterId = characterId,
                    Id = Guid.NewGuid(),
                    SkillIndex = skillIndex,
                    SkillExperience = skillExp,
                    SkillLevel = skillLevel,
                    SkillName = Skills.SkillNames[skillIndex]
                });
            }
            else
            {
                skillRecord.DateReached = DateTime.UtcNow;
                skillRecord.SkillLevel = skillLevel;
                skillRecord.SkillExperience = skillExp;
            }
        }

        private void BanUserAndCloseSession(DataModels.GameSession gameSession, CharacterSessionState characterSessionState, User sessionOwner)
        {
            characterSessionState.Compromised = true;
            sessionOwner.Status = 2;
            gameSession.Status = (int)SessionStatus.Inactive;
            gameSession.Stopped = DateTime.UtcNow;
        }

        public void EquipBestItems(Character character)
        {
            inventoryProvider.Get(character.Id).EquipBestItems();
        }

        internal void SendGameStateToTwitchExtension(SessionToken sessionToken, GameStateRequest gameState)
        {
            if (extensionWsConnectionProvider.TryGetAllByStreamer(sessionToken.UserId, out var connections))
            {
                foreach (var connection in connections)
                {
                    try
                    {
                        connection.SendAsync(gameState);
                    }
                    catch (Exception exc)
                    {
                        logger.LogError("Unable to send extension data to " + connection.Session?.UserName + ": " + exc);
                    }
                }
            }
        }

        internal async Task<bool> TrySendToExtensionAsync<T>(DataModels.GameSession session, Character character, T data)
        {
            if (extensionWsConnectionProvider.TryGetAllByStreamer(session.UserId, out var connections))
            {
                foreach (var connection in connections)
                {
                    if (connection.Session.UserId != character.UserId)
                    {
                        continue;
                    }
                    try
                    {
                        await connection.SendAsync(data);
                        return true;
                    }
                    catch (Exception exc)
                    {
                        logger.LogError("Unable to send extension data to " + character?.Name + " of type " + typeof(T).FullName + ": " + exc);
                    }
                }
            }

            return false;
        }
        internal async Task<bool> TrySendToExtensionAsync<T>(Character character, T data)
        {
            if (extensionWsConnectionProvider.TryGet(character.Id, out var connection))
            {
                try
                {
                    await connection.SendAsync(data);
                    return true;
                }
                catch (Exception exc)
                {
                    logger.LogError("Unable to send extension data to " + character?.Name + " of type " + typeof(T).FullName + ": " + exc);
                }
            }

            return false;
        }

        private void SendRemovePlayerFromSession(Character character, DataModels.GameSession gameSession, string reason)
        {
            if (gameSession == null)
            {
                return;
            }

            var targetSessionOwner = gameData.GetUser(gameSession.UserId);
            if (targetSessionOwner == null)
            {
                return;
            }

            var currentSessionOwner = character.UserIdLock != null ? gameData.GetUser(character.UserIdLock.Value) : null;

#if DEBUG
            var logMsg = currentSessionOwner != null
                ? $"Sent Remove Player {character.Name} to {targetSessionOwner.UserName}. Player is part of {currentSessionOwner.UserName}'s session. " + reason
                : $"Sent Remove Player {character.Name} to {targetSessionOwner.UserName}. Player is not part of any sessions.";

            logger.LogError(logMsg);
#endif

            var clientMessage = currentSessionOwner != null
                ? $"{character.Name} joined {currentSessionOwner.UserName}'s session."
                : $"{character.Name} joined another session.";

            var characterUser = gameData.GetUser(character.UserId);
            var gameEvent = gameData.CreateSessionEvent(GameEventType.PlayerRemove,
                gameSession,
                new PlayerRemove()
                {
                    Reason = clientMessage,
                    UserId = characterUser.Id,
                    CharacterId = character.Id
                });

            gameData.EnqueueGameEvent(gameEvent);
        }

        public void SendPlayerAddToSession(Character character, DataModels.GameSession gameSession)
        {
            var characterUser = gameData.GetUser(character.UserId);

            var uac = gameData.GetUserAccess(character.UserId);
            var platform = uac.FirstOrDefault();

            character.UserIdLock = gameSession.UserId;

            var gameEvent = gameData.CreateSessionEvent(GameEventType.PlayerAdd,
                gameSession,
                new PlayerAdd()
                {
                    Identifier = character.Identifier,
                    UserName = characterUser.UserName,
                    UserId = characterUser.Id,
                    CharacterId = character.Id,
                    Platform = platform?.Platform,
                    PlatformId = platform?.PlatformId,
                });

            gameData.EnqueueGameEvent(gameEvent);
        }

        //private User CreateUser(DataModels.GameSession session, PlayerJoinData playerData)
        //{
        //    var user = gameData.GetUserByTwitchId(playerData.UserId);
        //    if (user == null)
        //    {
        //        if (string.IsNullOrEmpty(playerData.UserId))
        //        {
        //            logger.LogError("Trying to create a new user but twitch user id is null.");
        //            return null;
        //        }

        //        if (string.IsNullOrEmpty(playerData.UserName))
        //        {
        //            logger.LogError("Trying to create a new user but username is null.");
        //            return null;
        //        }

        //        user = new User
        //        {
        //            Id = Guid.NewGuid(),
        //            UserId = playerData.UserId,
        //            UserName = playerData.UserName,
        //            Created = DateTime.UtcNow
        //        };
        //        gameData.Add(user);
        //    }
        //    CreateUserLoyalty(session, user, playerData);
        //    return user;
        //}

        private Task<Player> CreateUserAndPlayer(
            DataModels.GameSession session,
            PlayerJoinData playerData)
        {
            var user = playerData.UserId != Guid.Empty
                ? gameData.GetUser(playerData.UserId)
                : gameData.GetUser(playerData.PlatformId, playerData.Platform);

            if (user == null)
            {
                if (Guid.TryParse(playerData.PlatformId, out var characterId) && gameData.GetCharacter(characterId) != null)
                {
                    return null;
                }

                if (string.IsNullOrEmpty(playerData.PlatformId))
                {
                    logger.LogError("Trying to create a new user but twitch user id is null.");
                    return null;
                }

                if (string.IsNullOrEmpty(playerData.UserName))
                {
                    logger.LogError("Trying to create a new user but username is null.");
                    return null;
                }

                var resources = new Resources
                {
                    Id = Guid.NewGuid()
                };

                gameData.Add(resources);

                user = new User
                {
                    Id = Guid.NewGuid(),
                    UserId = playerData.PlatformId,
                    UserName = playerData.UserName,
                    Resources = resources.Id,
                    Created = DateTime.UtcNow
                };

                gameData.Add(user);

                gameData.Add(new UserAccess
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Platform = playerData.Platform,
                    PlatformId = playerData.PlatformId,
                    PlatformUsername = playerData.UserName,
                    Created = DateTime.UtcNow
                });
            }

            return CreatePlayer(session, user, playerData);
        }

        private Task<Player> CreatePlayer(DataModels.GameSession session, User user, PlayerJoinData playerData)
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

            logger.LogError("Creating new character for '" + user.UserName + "' Data: " + Newtonsoft.Json.JsonConvert.SerializeObject(playerData));

            var character = new Character
            {
                Id = Guid.NewGuid(),
                Name = user.UserName,
                UserId = user.Id,
                OriginUserId = session?.UserId ?? Guid.Empty,
                Created = DateTime.UtcNow,
                Identifier = playerData.Identifier,
                CharacterIndex = index
            };

            var syntyAppearance = GenerateRandomSyntyAppearance();

            var skills = gameData.GenerateSkills();
            var statistics = GenerateStatistics();
            var state = new DataModels.CharacterState()
            {
                Id = Guid.NewGuid(),
                Health = 10,
            };

            if (session != null)
            {
                CreateUserLoyalty(session, user, playerData);
            }

            gameData.Add(state);
            gameData.Add(syntyAppearance);
            gameData.Add(statistics);
            gameData.Add(skills);

            character.StateId = state.Id;
            character.SyntyAppearanceId = syntyAppearance.Id;
            character.StatisticsId = statistics.Id;
            character.SkillsId = skills.Id;
            character.LastUsed = DateTime.UtcNow;
            gameData.Add(character);

            return AddPlayerToSession(session, user, character);
        }

        private UserLoyalty CreateUserLoyalty(DataModels.GameSession session, User user, PlayerJoinData playerData = null)
        {
            if (session == null)
                return null;

            var loyalty = new UserLoyalty
            {
                Id = Guid.NewGuid(),
                Playtime = "00:00:00",
                Points = 0,
                Experience = 0,
                StreamerUserId = session.UserId,
                UserId = user.Id,
                Level = 1,
                CheeredBits = 0,
                GiftedSubs = 0,
                IsModerator = playerData?.Moderator ?? false,
                IsSubscriber = playerData?.Subscriber ?? false,
                IsVip = playerData?.Vip ?? false
            };
            gameData.Add(loyalty);
            return loyalty;
        }

        private UserLoyalty CreateUserLoyalty(Guid userId, Guid streamerId)
        {
            var loyalty = new UserLoyalty
            {
                Id = Guid.NewGuid(),
                Playtime = "00:00:00",
                Points = 0,
                Experience = 0,
                StreamerUserId = streamerId,
                UserId = userId,
                Level = 1,
                CheeredBits = 0,
                GiftedSubs = 0
            };

            gameData.Add(loyalty);
            return loyalty;
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


        private bool ValidateDateRange(DataModels.RedeemableItem redeemable)
        {
            if (string.IsNullOrEmpty(redeemable.AvailableDateRange))
                return true;

            var now = DateTime.UtcNow;
            if (redeemable.AvailableDateRange.Contains("=>"))
            {
                var data = redeemable.AvailableDateRange.Split("=>");
                var startDate = Parse(data[0]?.Trim());
                var endDate = Parse(data[1]?.Trim());

                if ((startDate.Year > 0 || endDate.Year > 0) && ((endDate.Year > 0 && now.Year > endDate.Year) || now.Year < startDate.Year))
                    return false;

                if ((startDate.Month > 0 || endDate.Month > 0) && ((endDate.Month > 0 && now.Month > endDate.Month) || now.Year < startDate.Month))
                    return false;

                if ((startDate.Day > 0 || endDate.Day > 0) && ((endDate.Day > 0 && now.Day > endDate.Day) || now.Year < startDate.Day))
                    return false;

                return true;
            }
            var date = Parse(redeemable.AvailableDateRange.Trim());
            if (date.Year > 0 && now.Year > date.Year) return false;
            if (date.Month > 0 && now.Month > date.Month) return false;
            if (date.Day > 0 && now.Day > date.Day) return false;
            return true;
        }

        private Date Parse(string str)
        {
            int year = 0;
            int month = 0;
            int day = 0;

            if (!string.IsNullOrEmpty(str))
            {
                var strData = str.Split('-');
                if (strData.Length > 0) int.TryParse(strData[0], out year);
                if (strData.Length > 1) int.TryParse(strData[1], out month);
                if (strData.Length > 2) int.TryParse(strData[2], out day);
            }

            return new Date
            {
                Year = year,
                Month = month,
                Day = day
            };
        }

        private struct Date
        {
            public int Year;
            public int Month;
            public int Day;
        }

        private Character GetCharacter(SessionToken token, string userId)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null) return null;

            var user = gameData.GetUserByTwitchId(userId);
            if (user == null) return null;

            var sessionCharacters = gameData.GetActiveSessionCharacters(session);
            return sessionCharacters.FirstOrDefault(x => x.UserId == user.Id);
        }

        private Character GetCharacter(SessionToken token, Guid characterId)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null) return null;
            var sessionCharacters = gameData.GetActiveSessionCharacters(session);
            return sessionCharacters.FirstOrDefault(x => x.Id == characterId);
        }

        private static string GetTaskArgumentBySkillIndex(int trainingSkillIndex)
        {
            if (trainingSkillIndex <= -1 || trainingSkillIndex >= Skills.SkillNames.Length) return null;
            var skillName = Skills.SkillNames[trainingSkillIndex];
            if (skillName == nameof(Skills.Health))
                return "All";
            if (skillName == nameof(Skills.Attack) ||
                skillName == nameof(Skills.Defense) ||
                skillName == nameof(Skills.Strength) ||
                skillName == nameof(Skills.Ranged) ||
                skillName == nameof(Skills.Healing) ||
                skillName == nameof(Skills.Magic))
                return skillName;
            return null;
        }

        private static string GetTaskBySkillIndex(int trainingSkillIndex)
        {
            if (trainingSkillIndex <= -1 || trainingSkillIndex >= Skills.SkillNames.Length) return null;
            var skillName = Skills.SkillNames[trainingSkillIndex];
            if (skillName == nameof(Skills.Health) ||
                skillName == nameof(Skills.Attack) ||
                skillName == nameof(Skills.Defense) ||
                skillName == nameof(Skills.Strength) ||
                skillName == nameof(Skills.Ranged) ||
                skillName == nameof(Skills.Healing) ||
                skillName == nameof(Skills.Magic))
                return "Fighting";
            return skillName;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasFlag(DataModels.CharacterState a, CharacterFlags b)
        {
            if (a.InRaid && b == CharacterFlags.InRaid) return true;
            if (a.InDungeon.GetValueOrDefault() && b == CharacterFlags.InDungeon) return true;
            if (a.InOnsen.GetValueOrDefault() && b == CharacterFlags.InOnsen) return true;
            if (a.IsCaptain.GetValueOrDefault() && b == CharacterFlags.IsCaptain) return true;
            if (a.InArena && b == CharacterFlags.InArena) return true;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasFlag(CharacterUpdate a, CharacterFlags b) => HasFlag(a.State, b);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasFlag(CharacterStateUpdate a, CharacterFlags b) => HasFlag(a.State, b);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasFlag(CharacterFlags a, CharacterFlags b) => (a & b) == b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetCharacterState(DataModels.CharacterState state, CharacterStateUpdate update)
        {
            var task = GetTaskBySkillIndex(update.TrainingSkillIndex);
            var taskArgument = GetTaskArgumentBySkillIndex(update.TrainingSkillIndex);
            if (string.IsNullOrEmpty(taskArgument)) taskArgument = task;

            state.Health = update.Health;
            state.InArena = HasFlag(update, CharacterFlags.InArena);
            state.InRaid = HasFlag(update, CharacterFlags.InRaid);
            state.InDungeon = HasFlag(update, CharacterFlags.InDungeon);
            state.JoinedDungeon = HasFlag(update, CharacterFlags.InDungeonQueue);
            state.InOnsen = HasFlag(update, CharacterFlags.InOnsen);
            state.IsCaptain = HasFlag(update, CharacterFlags.IsCaptain);
            state.Island = update.Island != Island.Ferry ? update.Island.ToString() : null;
            state.Destination = update.Destination != Island.Ferry ? update.Island.ToString() : null;
            state.ExpPerHour = update.ExpPerHour;
            state.EstimatedTimeForLevelUp = update.EstimatedTimeForLevelUp.ToString("yyyy-MM-ddTHH:mm:ss.fffffff");
            state.Task = task;
            state.TaskArgument = taskArgument ?? task;
            state.X = update.X;
            state.Y = update.Y;
            state.Z = update.Z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetCharacterState(DataModels.CharacterState state, CharacterUpdate update)
        {
            var task = update.Task;
            var taskArgument = update.TaskArgument;
            if (string.IsNullOrEmpty(taskArgument)) taskArgument = task;
            state.Health = update.Health;
            state.InArena = HasFlag(update, CharacterFlags.InArena);
            state.InRaid = HasFlag(update, CharacterFlags.InRaid);
            state.InDungeon = HasFlag(update, CharacterFlags.InDungeon);
            state.JoinedDungeon = HasFlag(update, CharacterFlags.InDungeonQueue);
            state.InOnsen = HasFlag(update, CharacterFlags.InOnsen);
            state.IsCaptain = HasFlag(update, CharacterFlags.IsCaptain);
            state.Island = update.Island != Island.Ferry ? update.Island.ToString() : null;
            state.Destination = update.Destination != Island.Ferry ? update.Island.ToString() : null;
            state.ExpPerHour = update.ExpPerHour;
            state.EstimatedTimeForLevelUp = update.EstimatedTimeForLevelUp.ToString();
            state.Task = task;
            state.TaskArgument = taskArgument;
            //state.IsCaptain = update.IsCaptain;
            state.X = update.X;
            state.Y = update.Y;
            state.Z = update.Z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static DataModels.CharacterState CreateCharacterState(CharacterStateUpdate update)
        {
            var task = GetTaskBySkillIndex(update.TrainingSkillIndex);
            var taskArgument = GetTaskArgumentBySkillIndex(update.TrainingSkillIndex);
            if (string.IsNullOrEmpty(taskArgument)) taskArgument = task;

            var state = new DataModels.CharacterState
            {
                Id = Guid.NewGuid(),
                Health = update.Health,
                InArena = HasFlag(update, CharacterFlags.InArena),
                InRaid = HasFlag(update, CharacterFlags.InRaid),
                InDungeon = HasFlag(update, CharacterFlags.InDungeon),
                InOnsen = HasFlag(update, CharacterFlags.InOnsen),
                JoinedDungeon = HasFlag(update, CharacterFlags.InDungeonQueue),
                IsCaptain = HasFlag(update, CharacterFlags.IsCaptain),
                Island = update.Island != Island.Ferry ? update.Island.ToString() : null,
                Destination = update.Destination != Island.Ferry ? update.Island.ToString() : null,
                ExpPerHour = update.ExpPerHour,
                EstimatedTimeForLevelUp = update.EstimatedTimeForLevelUp.ToString("yyyy-MM-ddTHH:mm:ss.fffffff"),
                Task = task,
                TaskArgument = taskArgument ?? task,
                X = update.X,
                Y = update.Y,
                Z = update.Z,
            };
            return state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static DataModels.CharacterState CreateCharacterState(CharacterUpdate update)
        {
            var task = update.Task;
            var taskArgument = update.TaskArgument;
            if (string.IsNullOrEmpty(taskArgument)) taskArgument = task;
            var state = new DataModels.CharacterState
            {
                Id = Guid.NewGuid(),
                Health = update.Health,
                InArena = HasFlag(update, CharacterFlags.InArena),
                InRaid = HasFlag(update, CharacterFlags.InRaid),
                InDungeon = HasFlag(update, CharacterFlags.InDungeon),
                InOnsen = HasFlag(update, CharacterFlags.InOnsen),
                JoinedDungeon = HasFlag(update, CharacterFlags.InDungeonQueue),
                IsCaptain = HasFlag(update, CharacterFlags.IsCaptain),
                Island = update.Island != Island.Ferry ? update.Island.ToString() : null,
                Destination = update.Destination != Island.Ferry ? update.Island.ToString() : null,
                ExpPerHour = update.ExpPerHour,
                EstimatedTimeForLevelUp = update.EstimatedTimeForLevelUp.ToString(),
                Task = task,
                TaskArgument = taskArgument,
                X = update.X,
                Y = update.Y,
                Z = update.Z,
            };
            return state;
        }

        private void UpdateResources(DataModels.GameSession session, Character character, DataModels.Resources resources)
        {
            var user = gameData.GetUser(character.UserId);
            var gameEvent = gameData.CreateSessionEvent(GameEventType.ResourceUpdate, session,
                new ResourceUpdate
                {
                    UserId = user.UserId,
                    CharacterId = character.Id,
                    FishAmount = resources.Fish,
                    OreAmount = resources.Ore,
                    WheatAmount = resources.Wheat,
                    WoodAmount = resources.Wood,
                    CoinsAmount = resources.Coins
                });

            gameData.EnqueueGameEvent(gameEvent);
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
                HelmetVisible = true,
                Cape = -1,
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

    }
}
