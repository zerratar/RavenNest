﻿using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Models;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Net.DeltaTcpLib;
using RavenNest.BusinessLogic.Twitch.Extension;
using RavenNest.DataModels;
using RavenNest.Models;
using RavenNest.Models.TcpApi;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Gender = RavenNest.DataModels.Gender;
using Item = RavenNest.DataModels.Item;
using Resources = RavenNest.DataModels.Resources;
using Skills = RavenNest.DataModels.Skills;
using Statistics = RavenNest.DataModels.Statistics;

namespace RavenNest.BusinessLogic.Game
{
    public class PlayerManager : IDeltaHandler
    {
        public const int Enchanting_CooldownCoinsPerSecond = 50;
        public const int AutoJoinDungeonCost = 5000;
        public const int AutoJoinRaidCost = 3000;
        public const int AutoRestCostPerSecond = 3;

        public const int MaxCharacterCount = 3;
        private readonly ILogger logger;
        private readonly IRavenBotApiClient ravenbotApi;
        private readonly IPlayerHighscoreProvider highscoreProvider;
        private readonly PlayerInventoryProvider inventoryProvider;
        private readonly EnchantmentManager enchantmentManager;
        private readonly GameData gameData;
        private readonly IIntegrityChecker integrityChecker;
        private readonly ITwitchExtensionConnectionProvider extensionWsConnectionProvider;

        private readonly ConcurrentDictionary<Guid, HashSet<Guid>> playerRemoveRequests = new ConcurrentDictionary<Guid, HashSet<Guid>>();

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


            if (u.IsModerator.GetValueOrDefault() || u.IsAdmin.GetValueOrDefault() || u.IsHiddenInHighscore.GetValueOrDefault())
            {
                return -2;
            }

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
                    user.DisplayName = Utility.SanitizeUserName(userName, userName);
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

#if DEBUG
                if (!result.Success && session != null)
                {
                    var sessionOwner = gameData.GetUser(session.UserId);
                    logger.LogError(("Unable to add player " + playerData.UserName + " to " + sessionOwner.UserName + "'s stream. " + (result.ErrorMessage + " " + internalErrorMessage).Trim()).Trim());
                }

#endif 
                if (result.Success && ravenbotApi != null)
                {
                    await ravenbotApi.UpdateUserSettingsAsync(result.Player.UserId);
                }
            }
        }

        private async Task SendUserRoleToRavenBotAsync(User user)
        {
            if (user == null || (!user.IsModerator.GetValueOrDefault() && !user.IsAdmin.GetValueOrDefault()))
            {
                return;
            }

            await ravenbotApi.UpdateUserSettingsAsync(user.Id);
        }

        public async Task<PlayerJoinResult> AddPlayerByCharacterId(DataModels.GameSession session, Guid characterId, bool isGameRestore = false, bool isForceAdd = false)
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
                if (!isForceAdd)
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
                    result.Players[i] = await AddPlayerByCharacterId(session, players.Characters[i], true, players.ForceAdd);
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

                if (character.UserIdLock != null && character.UserIdLock != session.UserId)
                {
                    SendRemovePlayerFromSessionToGame(character, session);
                }

                var sessionChars = gameData.GetActiveSessionCharacters(session);
                if (sessionChars != null && sessionChars.Count > 0)
                {
                    // get the characters from the same user, in case they have another character already on this stream.
                    // set them all to userIdLock = null. But also only if it is not the character we are joining with.
                    var charactersInSession = sessionChars.Where(x => x.UserId == user.Id && x.Id != character.Id).ToList();
                    foreach (var cs in charactersInSession)
                    {

                        if (cs.UserIdLock != null)
                            cs.PrevUserIdLock = cs.UserIdLock;

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
                if (character.UserIdLock != null)
                    character.PrevUserIdLock = character.UserIdLock;

                character.UserIdLock = session.UserId;


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

            // TODO: check if we have an active request to remove user from server.
            // then we can use this as a confirmation that the user was removed.
            if (HasPlayerRemoveRequest(characterId, session.UserId))
            {
                logger.LogDebug("Player " + character.Name + " was successfully removed from " + sessionOwner.UserName + "'s session after a remove request.");
                RemovePlayerRemoveRequest(characterId, session.UserId);
            }

            if (sessionOwner.Id != character.UserIdLock)
                return false;

            await this.TrySendToExtensionAsync(session, character, new PlayerRemove
            {
                CharacterId = characterId,
                Reason = "Left Game",
            });

            if (character.UserIdLock != null)
                character.PrevUserIdLock = character.UserIdLock;
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
                if (character.UserIdLock != null)
                    character.PrevUserIdLock = character.UserIdLock;

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

        public void AddPlayerRemoveRequest(Guid characterId, Guid sessionUserId)
        {
            playerRemoveRequests.TryGetValue(sessionUserId, out var reqs);
            if (reqs == null)
            {
                reqs = new HashSet<Guid>();
            }
            reqs.Add(characterId);
            playerRemoveRequests[sessionUserId] = reqs;
        }

        public bool HasPlayerRemoveRequest(Guid characterId, Guid sessionUserId)
        {
            playerRemoveRequests.TryGetValue(sessionUserId, out var reqs);
            if (reqs == null)
                return false;
            return reqs.Contains(characterId);
        }

        public void RemovePlayerRemoveRequest(Guid characterId, Guid sessionUserId)
        {
            playerRemoveRequests.TryGetValue(sessionUserId, out var reqs);
            if (reqs == null)
                return;

            reqs.Remove(characterId);
            playerRemoveRequests[sessionUserId] = reqs;
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

            AddPlayerRemoveRequest(character.Id, currentSession.UserId);

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

        public ItemUseResult UseItem(SessionToken sessionToken, Guid characterId, Guid inventoryItemId, string arg)
        {
            var character = GetCharacter(sessionToken, characterId);
            if (character == null)
            {
                logger.LogError("Unable to use item (invId: " + inventoryItemId + ") Character not found in session: " + sessionToken.SessionId);
                return null;
            }

            var inventory = inventoryProvider.Get(character.Id);
            var item = inventory.Get(inventoryItemId);
            if (item.IsNull())
            {
                logger.LogError("Unable to use item (invId: " + inventoryItemId + ") Item does not exist.");
                return null;
            }

            var knownItems = gameData.GetKnownItems();
            if (item.ItemId == knownItems.TomeOfTeleportation.Id)
            {
                if (string.IsNullOrEmpty(arg) ||
                    !Enum.TryParse<RavenNest.Models.Island>(arg, true, out var parsedIsland) ||
                    parsedIsland == RavenNest.Models.Island.None ||
                    parsedIsland == RavenNest.Models.Island.Ferry ||
                    parsedIsland == RavenNest.Models.Island.Any)
                {
                    // not a suitable combination as this tome requires an argument to be used.
                    return null;
                }
            }

            if (item.Item.Type != (int)DataModels.ItemType.Food &&
                item.Item.Type != (int)DataModels.ItemType.Scroll &&
                item.Item.Type != (int)DataModels.ItemType.Potion)
            {
                return null;
            }

            var itemEffects = gameData.GetItemStatusEffects(item.ItemId);
            if (itemEffects.Count == 0)
            {
                return null;
            }

            var skills = gameData.GetCharacterSkills(character.SkillsId);
            if (inventory.RemoveItem(item, 1))
            {
                var shouldTeleport = false;
                var island = RavenNest.Models.Island.None;
                var effects = new List<RavenNest.Models.CharacterStatusEffect>();

                foreach (var fx in itemEffects)
                {
                    if (fx.Type == DataModels.StatusEffectType.TeleportToIsland)
                    {
                        shouldTeleport = true;
                        island = Enum.Parse<RavenNest.Models.Island>(arg, true);
                        continue;
                    }

                    effects.Add(CreateCharacterEffect(character.Id, skills, fx));
                }

                item = inventory.Get(inventoryItemId); // if it does not exist amount will be 0
                var result = new ItemUseResult
                {
                    InventoryItemId = inventoryItemId,
                    NewStackAmount = (int)item.Amount,
                    Effects = effects,
                    Teleport = shouldTeleport,
                    EffectIsland = island
                };


                return result;
            }

            return null;
        }

        private RavenNest.Models.CharacterStatusEffect CreateCharacterEffect(Guid characterId, Skills skills, ItemStatusEffect fx)
        {
            // if this is an effect we should store, then also create a datamodel version and add to the gameData.
            // but we will always replace existing effects with these ones.
            var now = DateTime.UtcNow;
            var activeEffects = gameData.GetCharacterStatusEffects(characterId);
            foreach (var ae in activeEffects)
            {
                if (ae.Type == fx.Type)
                {
                    // replace
                    ae.StartUtc = DateTime.UtcNow;
                    ae.ExpiresUtc = ae.StartUtc.AddSeconds(fx.Duration);
                    ae.Amount = DetermineEffectAmount(skills, fx.Type, fx.Amount, fx.MinAmount);
                    ae.LastUpdateUtc = now;
                    ae.Duration = fx.Duration;
                    ae.TimeLeft = fx.Duration;
                    return new RavenNest.Models.CharacterStatusEffect
                    {
                        Type = (RavenNest.Models.StatusEffectType)fx.Type,
                        Amount = (float)ae.Amount,
                        ExpiresUtc = ae.ExpiresUtc,
                        StartUtc = ae.StartUtc,
                        TimeLeft = fx.Duration,
                        Duration = fx.Duration,
                        LastUpdateUtc = now,
                    };
                }
            }


            var effect = new RavenNest.DataModels.CharacterStatusEffect
            {
                StartUtc = now,
                ExpiresUtc = now.AddSeconds(fx.Duration),
                Amount = DetermineEffectAmount(skills, fx.Type, fx.Amount, fx.MinAmount),
                LastUpdateUtc = now,
                TimeLeft = fx.Duration,
                Duration = fx.Duration,
                CharacterId = characterId,
                Id = Guid.NewGuid(),
                Type = fx.Type
            };

            gameData.Add(effect);

            return new RavenNest.Models.CharacterStatusEffect
            {
                Type = (RavenNest.Models.StatusEffectType)fx.Type,
                Amount = (float)effect.Amount,
                TimeLeft = fx.Duration,
                Duration = fx.Duration,
                LastUpdateUtc = now,
                ExpiresUtc = effect.ExpiresUtc,
                StartUtc = effect.StartUtc,
            };
        }

        private double DetermineEffectAmount(Skills skills, DataModels.StatusEffectType type, double amount, double minAmount)
        {
            double scaledValue = 0;
            double value = 0;
            switch (type)
            {
#warning Implement the rest of the status effect amount, damages need to be adjusted too.
                case DataModels.StatusEffectType.Heal:
                case DataModels.StatusEffectType.HealOverTime:
                    value = skills.HealthLevel;
                    scaledValue = (skills.HealthLevel * amount);
                    break;
            }

            if (scaledValue < minAmount && value > 0)
            {
                return minAmount / value;
            }

            return amount;
        }

        public RedeemItemResult RedeemItem(SessionToken sessionToken, Guid characterId, Guid itemId)
        {
            var character = GetCharacter(sessionToken, characterId);
            if (character == null)
            {
                logger.LogError("Unable to redeem item for " + characterId + ". Character not found in session: " + sessionToken.SessionId);
                return RedeemItemResult.Error("Player not in session.");
            }

            try
            {
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
                var currencyItem = gameData.GetItem(redeemable.CurrencyItemId);
                var stashCurrencyItem = gameData.GetStashItem(character.UserId, redeemable.CurrencyItemId);
                var stashCurrencyAmount = stashCurrencyItem != null ? stashCurrencyItem.Amount : 0;
                var currencyInvItem = inventory.GetByItemId(redeemable.CurrencyItemId);
                //if ((currencyItem.Amount + stashCurrencyAmount) < redeemable.Cost)
                if ((currencyInvItem.Amount + stashCurrencyAmount) < redeemable.Cost)
                {
                    var insufficient = RedeemItemResult.InsufficientCurrency(currencyInvItem.Amount, redeemable.Cost, currencyItem.Name);
                    insufficient.CurrencyItemId = redeemable.CurrencyItemId;
                    insufficient.RedeemedItemId = redeemable.ItemId;
                    insufficient.CurrencyLeft = currencyInvItem.Amount + stashCurrencyAmount;
                    insufficient.CurrencyCost = redeemable.Cost;
                    return insufficient;
                }

                DataModels.InventoryItem added = null;
                // if we don't have any in inventory, we should only remove from the stash
                if (currencyInvItem.Amount > 0)
                {
                    if (inventory.RemoveItem(currencyInvItem, redeemable.Cost, out var toRemove))
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

                        added = inventory.AddItem(redeemable.ItemId, Math.Max(1, redeemable.Amount)).FirstOrDefault();
                    }

                    SendItemRemoveEvent(new DataModels.InventoryItem
                    {
                        ItemId = redeemable.CurrencyItemId,
                    }, redeemable.Cost > currencyInvItem.Amount ? currencyInvItem.Amount : redeemable.Cost, character);
                }
                else
                {
                    if (gameData.RemoveFromStash(stashCurrencyItem, redeemable.Cost))
                    {
                        added = inventory.AddItem(redeemable.ItemId, Math.Max(1, redeemable.Amount)).FirstOrDefault();
                    }
                }

                SendItemAddEvent(new DataModels.InventoryItem
                {
                    ItemId = redeemable.ItemId,
                    Id = added?.Id ?? Guid.Empty,
                    CharacterId = character.Id,
                    Amount = added?.Amount ?? 0,
                    Soulbound = false
                }, redeemable.Amount, character);

                return new RedeemItemResult
                {
                    Code = RedeemItemResultCode.Success,
                    InventoryItemId = added?.Id ?? Guid.Empty,
                    RedeemedItemAmount = redeemable.Amount,
                    RedeemedItemId = redeemable.ItemId,
                    CurrencyItemId = redeemable.CurrencyItemId,
                    CurrencyCost = redeemable.Cost,
                    CurrencyLeft = inventory.GetByItemId(redeemable.CurrencyItemId).Amount,
                };
            }
            catch (Exception exc)
            {
                var targetItem = gameData.GetItem(itemId);
                if (targetItem != null)
                {
                    logger.LogError(character.Name + " is unable to redeem a " + targetItem.Name + ": " + exc);
                }
                else
                {
                    logger.LogError(character.Name + " is unable to redeem item with ID: " + itemId + " " + exc);
                }

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

        [Obsolete]
        public ItemEnchantmentResult EnchantItemInstance(SessionToken sessionToken, string userId, Guid inventoryItemId)
        {
            return EnchantItem(sessionToken, userId, inventoryItemId);
        }

        public ItemEnchantmentResult EnchantItemInstance(SessionToken sessionToken, Guid characterId, Guid inventoryItemId)
        {
            var character = GetCharacter(sessionToken, characterId);

            return EnchantItem(sessionToken, inventoryItemId, character);
        }

        [Obsolete]
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

        public ItemProductionResult ProduceItems(SessionToken sessionToken, Guid characterId, Guid recipeId, int amount)
        {
            var result = new ItemProductionResult();
            result.Success = true;
            if (amount <= 0) amount = 1;
            amount = Math.Min(50_000_000, amount);

            // make sure the character is part of the target session.
            var character = GetCharacter(sessionToken, characterId);
            if (character == null) return null;

            // To ensure we don't mess with the stash right now, we will only use ingredients from inventory
            // this should be updated later.

            var recipe = gameData.GetItemRecipe(recipeId);
            if (recipe == null) return null;

            var skills = gameData.GetCharacterSkills(character.SkillsId);
            var skillBonuses = GetSkillBonuses(character);

            var craftingLevel = (int)(skills.CraftingLevel + skillBonuses[RavenNest.Models.Skill.Crafting]);
            var alchemyLevel = (int)(skills.AlchemyLevel + skillBonuses[RavenNest.Models.Skill.Alchemy]);
            var cookingLevel = (int)(skills.CookingLevel + skillBonuses[RavenNest.Models.Skill.Cooking]);

            var skillLevel = 1;
            // check required skill level. This will include bonus from enchantments.
            var requiredSkill = (RavenNest.Models.Skill)recipe.RequiredSkill;
            if (requiredSkill == RavenNest.Models.Skill.Crafting) skillLevel = craftingLevel;
            if (requiredSkill == RavenNest.Models.Skill.Alchemy) skillLevel = alchemyLevel;
            if (requiredSkill == RavenNest.Models.Skill.Cooking) skillLevel = cookingLevel;
            if (recipe.RequiredLevel > skillLevel)
                return result;

            // next is to check for each item we try to craft, we can remove the needed ingredients.
            var inventory = inventoryProvider.Get(character.Id);

            // this will be simpler than trying to calculate the possible crafting count. It will be less effective performance wise but also less bug prone.
            var ingredients = gameData.GetRecipeIngredients(recipeId);

            if (ingredients.Count == 0)
            {
                // this must be a bug or data issue. DO NOT PRODUCE!
                return null;
            }

            var tmpList = new List<ItemProductionResultItem>();

            for (var i = 0; i < amount; ++i)
            {
                // try crafting one item at a time.
                // if it was successful, it will be added to inventory there.
                var producedItem = ProduceItem(skillLevel, inventory, recipe, ingredients);

                // if this is null, we did not have enough resources to produce the item.
                if (producedItem == null)
                {
                    break;
                }

                tmpList.Add(producedItem);
            }

            result.Items = new List<ItemProductionResultItem>();

            // since we could end up with a ton of item rows, lets merge them. This could have been avoided if we passed along the amount of items we wanted to produce.
            // but this is still a low cost compared to the complexity added otherwise.
            foreach (var i in tmpList.GroupBy(x => x.InventoryItemId))
            {
                var inventoryItemId = i.Key; // we can group by inventory item id since it should share the same one if the item id is the same.
                var addedAmount = i.Sum(x => x.Amount);
                var stackAmount = i.Max(x => x.StackAmount); // the last item should have the correct stack amount. but we can just take max for simplicity.
                var first = i.First();
                var success = first.Success;
                var itemId = first.ItemId;
                result.Items.Add(new ItemProductionResultItem
                {
                    Success = success,
                    StackAmount = stackAmount,
                    Amount = addedAmount,
                    InventoryItemId = inventoryItemId,
                    ItemId = itemId,
                });
            }

            return result;
        }

        private ItemProductionResultItem ProduceItem(int skillLevel, PlayerInventory inventory, DataModels.ItemRecipe recipe, IReadOnlyList<DataModels.ItemRecipeIngredient> ingredients)
        {
            // go through all ingredients and make sure we can remove it from the player inventory.
            var result = new ItemProductionResultItem();

            foreach (var ingredient in ingredients)
            {
                var i = inventory.GetByItemId(ingredient.ItemId);
                if (i.Amount < ingredient.Amount)
                    return null;// we don't have sufficient resources, return null.
            }

            // consume ingredients.
            var removedItems = new List<ReadOnlyInventoryItem>();
            foreach (var ingredient in ingredients)
            {
                // we should not have multiple stacks of the same item. so lets just get THE item.
                var i = inventory.GetByItemId(ingredient.ItemId);

                // it shouldnt happen, but in case it does. revert the removed items and return null.
                if (!inventory.RemoveItem(i, ingredient.Amount))
                {
                    if (removedItems.Count > 0)
                    {
                        foreach (var item in removedItems)
                        {
                            inventory.TryAddItem(item, item.Amount);
                        }
                    }
                    return null;
                }
                removedItems.Add(i);
            }

            // all good. no need to keep the removed items in memory.
            removedItems.Clear();

            void AddItem(Guid itemId, bool success)
            {
                var stack = inventory.AddItem(itemId)[0];
                result.InventoryItemId = stack.Id;
                result.ItemId = stack.ItemId;
                result.StackAmount = stack.Amount ?? 1;
                result.Amount = 1;
                result.Success = success;
            }

            var canFail = recipe.FailedItemId != null && recipe.MinSuccessRate < 1;
            if (!canFail)
            {
                // this one can't fail. So we will just create the target item.
                AddItem(recipe.ItemId, true);
            }
            else
            {
                // if we can fail, we need to determine whether or not this was a success.
                var rng = System.Random.Shared.NextDouble();
                var isSuccess = rng <= System.Math.Max(recipe.MinSuccessRate, recipe.MaxSuccessRate);

                if (!recipe.FixedSuccessRate)
                {
                    isSuccess = rng <= GameMath.CalculateSuccessRate(skillLevel, recipe.RequiredLevel, recipe.MinSuccessRate, recipe.MaxSuccessRate);
                }

                AddItem(isSuccess ? recipe.ItemId : recipe.FailedItemId.Value, isSuccess);
            }

            return result;
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

            var inventory = inventoryProvider.Get(character.Id);
            var craftingBonus = inventory.GetCraftingBonus();
            var craftingLevel = skills.CraftingLevel + craftingBonus;

            if (craftingLevel < item.RequiredCraftingLevel)
                return CraftItemResult.TooLowLevel(item.Id, item.RequiredCraftingLevel);

            if (!CanCraftItems(item, resources, craftingLevel, amount, out var craftableAmount))
                return CraftItemResult.InsufficientResources;

            var result = new CraftItemResult();
            var craftingRequirements = gameData.GetCraftingRequirements(itemId);

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
        private Dictionary<RavenNest.Models.Skill, double> GetSkillBonuses(Character character)
        {
            var inventory = inventoryProvider.Get(character.Id);
            return inventory.GetSkillBonuses();
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

            var inventoryItems = inventory.AddItem(itemId, amount, tag: item.Tag,
                enchantment: item.Enchantment, name: item.Name,
                transmogrificationId: item.TransmogrificationId, flags: item.Flags);

            // Remove the item from the marketplace
            gameData.Remove(item);

            // Send the update to the game client and extensions
            SendItemAddEvent(inventoryItems[0], (int)amount, character);

            return true;
        }

        private void SendItemSyncEvent(Character character, ReadOnlyInventoryItem[] items)
        {
            var sessionUserId = character.UserIdLock;
            if (sessionUserId == null)
                return;

            var session = gameData.GetSessionByUserId(sessionUserId.Value);
            if (session != null)
            {
                var data = new ItemSync
                {
                    PlayerId = character.Id,
                    Items = items.Select(x => ModelMapper.Map(x.AsUntrackedInventoryItem(character.Id))).ToList()
                };

                gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.ItemSync, session, data));
                TrySendToExtensionAsync(character, data);
            }

            //var data = new ItemAppearance
            //{
            //    PlayerId = character.Id,
            //    InventoryItemId = inventoryItemId,
            //    TransmogrificationId = transmogrifactionId
            //    ConsumedItemId = inventoryItemId,
            //};


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
                Soulbound = item.Soulbound,
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

        private async Task SendRemoveItemsByCategoryEventAsync(Character character, ItemFilter filter, List<Guid> exclude)
        {
            var data = new ItemRemoveByCategory
            {
                PlayerId = character.Id,
                Filter = (RavenNest.Models.ItemFilter)filter,
                Exclude = exclude,
            };

            var sessionUserId = character.UserIdLock;
            if (sessionUserId != null)
            {
                var session = gameData.GetSessionByUserId(sessionUserId.Value);
                if (session != null)
                {
                    gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.ItemRemoveByCategory, session, data));
                }
            }

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


        public AddItemInstanceResult AddItemInstanceDetailed(SessionToken token, string userId, RavenNest.Models.AddItemRequest instance)
        {
            var character = GetCharacter(token, userId);
            if (character == null)
                return AddItemInstanceResult.BadCharacter(userId);

            return AddItemToCharacter(token, instance, character);
        }

        public AddItemInstanceResult AddItem(SessionToken token, Guid characterId, RavenNest.Models.AddItemRequest instance)
        {
            var character = GetCharacter(token, characterId);
            if (character == null)
                return AddItemInstanceResult.Failed();

            return AddItemToCharacter(token, instance, character);
        }

        private AddItemInstanceResult AddItemToCharacter(SessionToken token, RavenNest.Models.AddItemRequest instance, Character character)
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

        public Guid AddItemInstance(SessionToken token, string userId, RavenNest.Models.AddItemRequest instance)
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
            if (itemToVendor.Item == null) return 0;
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
        public bool SellItemToVendor(Guid characterId, ItemFilter filter, IReadOnlyList<RavenNest.Models.InventoryItem> items)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null) return false;
            var inventory = inventoryProvider.Get(character.Id);

            DataModels.Resources resources = null;
            DataModels.GameSession session = null;

            resources = gameData.GetResources(character);
            if (resources == null) return false;

            var exclude = new List<Guid>();
            var isSuccess = false;
            foreach (var invItem in items)
            {
                if (inventory.IsLocked(invItem.Id))
                {
                    exclude.Add(invItem.Id);
                    continue;
                }

                var stack = gameData.GetInventoryItem(invItem.Id);
                if (stack == null)
                {
                    exclude.Add(invItem.Id);
                    continue;
                }

                var amountToVendor = stack.Amount.GetValueOrDefault();
                var i = gameData.GetItem(stack.ItemId);
                if (amountToVendor == 0 || i == null || i.Category == (int)DataModels.ItemCategory.StreamerToken)
                {
                    exclude.Add(invItem.Id);
                    continue;
                }

                if (inventory.RemoveItem(stack, amountToVendor))
                {
                    isSuccess = true;
                    var price = i.ShopSellPrice * amountToVendor;
                    resources.Coins += price;
                    UpdateStockAndLogTransaction(characterId, stack.ItemId, amountToVendor, price, false);
                }
            }

            var sessionUserId = character.UserIdLock;
            if (sessionUserId == null) return isSuccess;
            session = gameData.GetSessionByUserId(sessionUserId.Value);
            if (session == null) return isSuccess;

            UpdateResources(session, character, resources);
            SendRemoveItemsByCategoryEventAsync(character, filter, exclude);
            return isSuccess;
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

                UpdateStockAndLogTransaction(characterId, item.ItemId, amountToVendor, price, false);

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


        public bool RemoveTransmogrification(Guid characterId, RavenNest.Models.InventoryItem item)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null) return false;
            var inventoryItem = gameData.GetInventoryItem(item.Id);
            var inventory = inventoryProvider.Get(character.Id);
            if (inventory.RemoveTransmogrification(inventoryItem, out var affectedItemStacks))
            {
                SendItemSyncEvent(character, affectedItemStacks);
                return true;
            }

            return false;
        }

        public bool AddTransmogrification(Guid characterId, RavenNest.Models.InventoryItem item, Guid transmogrificationId)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null) return false;
            var inventoryItem = gameData.GetInventoryItem(item.Id);
            var inventory = inventoryProvider.Get(character.Id);
            if (inventory.AddTransmogrification(inventoryItem, transmogrificationId, out var affectedItemStacks))
            {
                SendItemSyncEvent(character, affectedItemStacks);
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

        public GiftItemResult SendInventoryItem(SessionToken sessionToken, Guid gifterCharacterId, string alias, Guid inventoryItemId, long amount)
        {
            var gifter = GetCharacter(sessionToken, gifterCharacterId);
            if (gifter == null || !integrityChecker.VerifyPlayer(sessionToken.SessionId, gifter.Id, 0)) return GiftItemResult.Error;

            var receiver = gameData.GetCharacterByUserId(gifter.UserId, alias);
            if (receiver == null || gifter.Id == receiver.Id) return GiftItemResult.Error;
            return GiftInventoryItem(inventoryItemId, amount, gifter, receiver);
        }


        public GiftItemResult GiftInventoryItem(SessionToken sessionToken, Guid gifterUserId, Guid receiverUserId, Guid inventoryItemId, long amount)
        {
            var gifter = GetCharacter(sessionToken, gifterUserId);
            if (gifter == null || !integrityChecker.VerifyPlayer(sessionToken.SessionId, gifter.Id, 0)) return GiftItemResult.Error;
            var receiver = GetCharacter(sessionToken, receiverUserId);
            if (receiver == null) return GiftItemResult.Error;
            return GiftInventoryItem(inventoryItemId, amount, gifter, receiver);
        }

        private GiftItemResult GiftInventoryItem(Guid inventoryItemId, long amount, Character gifter, Character receiver)
        {
            var inventory = inventoryProvider.Get(gifter.Id);
            var item = inventory.Get(inventoryItemId);
            if (item.IsNull() || item.Soulbound || item.TransmogrificationId != null) return GiftItemResult.SoulboundItem;
            var gift = item;
            if (inventory.IsLocked(gift.Id)) return GiftItemResult.InventoryError;
            var recvInventory = inventoryProvider.Get(receiver.Id);
            var amountToGift = gift.Amount >= amount ? amount : (int)gift.Amount;
            if (recvInventory.TryAddItem(gift, amountToGift, out var result) &&
                inventory.TryRemoveItem(gift, amountToGift, out var old))
            {
                return GiftItemResult.OK(amountToGift, ModelMapper.Map(result), ModelMapper.Map(old));
            }
            return GiftItemResult.NoItem;
        }

        public long SendCoins(SessionToken sessionToken, Guid senderCharacterId, Guid receiverCharacterId, long amount)
        {
            var character = gameData.GetCharacter(senderCharacterId);
            var targetCharacter = gameData.GetCharacter(receiverCharacterId);
            if (amount <= 0 || character == null || targetCharacter == null) return -1;

            var res = gameData.GetResources(character);
            var tarRes = gameData.GetResources(targetCharacter);

            if (res.Coins <= 0) return -2;
            var toGive = res.Coins < amount ? (long)res.Coins : amount;
            res.Coins -= toGive;
            tarRes.Coins += toGive;
            return toGive;
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

                    gameData.Add(GameData.CreateBankItem(character.UserId, stack, amount));
                    return true;
                }
                finally
                {
                    SendItemRemoveEvent(stack, amount, character, true);
                }
            }

            return false;
        }


        public void SendToStash(Guid characterId, ItemFilter filter)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null) return;
            var inventory = inventoryProvider.Get(character.Id);

            var unequipped = inventory.GetUnequippedItems();
            var exclude = new List<Guid>();
            foreach (var u in unequipped)
            {
                if (filter != ItemFilter.All)
                {
                    var a = GameData.GetItemFilter(u.Item);
                    if (a != filter)
                    {
                        // why is excluding items of "wrong" filter? this will automatically not be removed on the client.
                        // we should only exclude items if they have a reason they can't be "moved" when in same filter.
                        //exclude.Add(u.Id);
                        continue;
                    }
                }

                if (inventory.IsLocked(u.Id)) continue;
                var stack = gameData.GetInventoryItem(u.Id);
                var amount = stack.Amount.GetValueOrDefault();
                if (inventory.RemoveItem(stack, amount))
                {
                    var canBeStacked = PlayerInventory.CanBeStacked(stack);
                    if (canBeStacked)
                    {
                        var bankItems = gameData.GetUserBankItems(character.UserId);
                        var existing = bankItems.FirstOrDefault(x => PlayerInventory.CanBeStacked(x, stack));
                        if (existing != null)
                        {
                            existing.Amount += amount;
                            continue;
                        }
                    }
                    gameData.Add(GameData.CreateBankItem(character.UserId, stack, amount));
                }
            }

            SendRemoveItemsByCategoryEventAsync(character, filter, exclude);
        }

        public ClearEnchantmentCooldownResult ClearEnchantmentCooldown(SessionToken sessionToken, Guid characterId)
        {
            var character = GetCharacter(sessionToken, characterId);
            if (character == null) return new ClearEnchantmentCooldownResult();
            var res = gameData.GetResources(character);
            var cd = gameData.GetEnchantmentCooldown(character.Id);
            if (cd.CooldownEnd <= DateTime.UtcNow) return new ClearEnchantmentCooldownResult { Success = true };

            var secondsLeft = (cd.CooldownEnd - DateTime.UtcNow).TotalSeconds;
            var cost = (long)(Enchanting_CooldownCoinsPerSecond * secondsLeft);
            if (cost > res.Coins) return new ClearEnchantmentCooldownResult();
            res.Coins -= cost;
            cd.CooldownEnd = DateTime.UtcNow;
            return new ClearEnchantmentCooldownResult { Success = true, TotalCost = cost };
        }

        public EnchantmentCooldownResult GetEnchantmentCooldown(SessionToken sessionToken, Guid characterId)
        {
            var character = GetCharacter(sessionToken, characterId);
            if (character == null) return new EnchantmentCooldownResult();
            var cd = gameData.GetEnchantmentCooldown(character.Id);
            if (cd.CooldownEnd <= DateTime.UtcNow) return new EnchantmentCooldownResult();
            return new EnchantmentCooldownResult
            {
                Cooldown = cd.CooldownEnd,
                CoinsPerSeconds = Enchanting_CooldownCoinsPerSecond
            };
        }

        public bool[] AutoJoinRaid(SessionToken sessionToken, IReadOnlyList<Guid> characters)
        {
            var successCount = 0;
            var arr = new bool[characters.Count];
            for (var i = 0; i < arr.Length; ++i)
            {
                arr[i] = AutoJoinRaid(sessionToken, characters[i]);
                if (arr[i]) successCount++;
            }
            if (characters.Count > successCount)
            {
                logger.LogWarning("Auto Join Raid failed for {0} characters, out of a total {1}, session {2}", characters.Count - successCount, characters.Count, sessionToken.UserName);
            }
            else
            {
                logger.LogWarning("[{0}] {1} characters auto joined raid.", sessionToken.UserName, characters.Count);
            }
            return arr;
        }

        public bool[] AutoJoinDungeon(SessionToken sessionToken, IReadOnlyList<Guid> characters)
        {
            var successCount = 0;
            var arr = new bool[characters.Count];
            for (var i = 0; i < arr.Length; ++i)
            {
                arr[i] = AutoJoinDungeon(sessionToken, characters[i]);
                if (arr[i]) successCount++;
            }
            if (characters.Count > successCount)
            {
                logger.LogWarning("Auto Join Dungeon failed for {0} characters, out of a total {1}, session {2}", characters.Count - successCount, characters.Count, sessionToken.UserName);
            }
            else
            {
                logger.LogWarning("[{0}] {1} characters auto joined dungeon.", sessionToken.UserName, characters.Count);
            }
            return arr;
        }

        public bool AutoJoinDungeon(SessionToken sessionToken, Guid characterId)
        {
            try
            {
                var character = GetCharacter(sessionToken, characterId);
                if (character == null) return false;

                var res = gameData.GetResources(character);
                var cost = AutoJoinDungeonCost;
                if (cost > res.Coins) return false;
                res.Coins -= cost;
                return true;
            }
            catch (Exception exc)
            {
                logger.LogError($"Character with Id {characterId}, session: {sessionToken.UserName} failed to auto join dungeon: {exc}");
            }
            return false;
        }

        public bool AutoJoinRaid(SessionToken sessionToken, Guid characterId)
        {
            try
            {
                var character = GetCharacter(sessionToken, characterId);
                if (character == null) return false;

                var res = gameData.GetResources(character);
                var cost = AutoJoinRaidCost;
                if (cost > res.Coins) return false;
                res.Coins -= cost;
                return true;
            }
            catch (Exception exc)
            {
                logger.LogError($"Character with Id {characterId}, session: {sessionToken.UserName} failed to auto join raid: {exc}");
            }
            return false;
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


        public void OnGameState(SessionToken session, Net.DeltaTcpLib.GameStateRequest state)
        {
            if (extensionWsConnectionProvider.TryGetAllByStreamer(session.UserId, out var connections))
            {
                foreach (var connection in connections)
                {
                    try
                    {
                        connection.SendAsync(state);
                    }
                    catch (Exception exc)
                    {
                        logger.LogError("Unable to send extension data to " + connection.Session?.UserName + ": " + exc);
                    }
                }
            }
        }


        public void OnPlayerStateDelta(SessionToken sessionToken, IReadOnlyList<CharacterStateDelta> deltas)
        {
            if (sessionToken == null || deltas == null || deltas.Count == 0)
            {
                return;
            }

            var sessionOwner = gameData.GetUser(sessionToken.UserId);
            if (sessionOwner == null)
            {
                logger.LogError("Unable to find session owner for " + sessionToken.UserName);
                return;
            }

            if (sessionOwner.Status >= 1)
            {
                logger.LogError("The user session from " + sessionOwner.UserName + " trying to save player states, but the owner has been banned.");
                return;
            }

            var gameSession = gameData.GetSession(sessionToken.SessionId);
            if (gameSession == null)
            {
                gameSession = GameData.CreateSession(sessionToken.UserId, sessionToken.SessionId);
                gameData.Add(gameSession);
            }

            var sessionState = gameData.GetSessionState(gameSession.Id);
            if (sessionState != null)
            {
                if (string.IsNullOrEmpty(sessionState.ClientVersion))
                    sessionState.ClientVersion = sessionToken.ClientVersion;

                sessionState.LastStateRequest = DateTime.UtcNow;
            }

            foreach (var data in deltas)
            {
                try
                {
                    if (data.DirtyMask == 0)
                    {
                        continue;
                    }

                    var character = gameData.GetCharacter(data.CharacterId);
                    if (character == null)
                    {
                        logger.LogError("Trying to update a character that does not exist. " + data.CharacterId + " (" + data.PlatformUserName + "). Creating a new one.");
                        //character = CreateMissingCharacter(data, gameSession);
                        //
                        //SendRemovePlayerFromSession(delta.CharacterId, gameSession, "Character with ID " + delta.CharacterId + " does not exist.");
                        continue;
                    }

                    // take ownership of the character if its not part of a stream
                    if (character.UserIdLock == null)
                    {
                        character.UserIdLock = sessionOwner.Id;
                    }

                    // check if we are part of this session, if not, we should attempt to remove the character from the stream.
                    if (character.UserIdLock != sessionOwner.Id)
                    {
                        try
                        {
                            if (TryMoveToSession(character, sessionOwner))
                            {
                                continue;
                            }

                            var characterSessionOwner = character.UserIdLock != null ? gameData.GetUser(character.UserIdLock.GetValueOrDefault()) : null;
                            var partOfSession = characterSessionOwner != null ? characterSessionOwner.UserName : "";

                            logger.LogError(character.Name + " is getting state updates from '" + sessionOwner.UserName + "' but is actually part of '" + partOfSession + "'. Update has been ignored.");
                            SendRemovePlayerFromSession(character, gameSession, "Character is part of another session.");
                            continue;
                            // send remove from this session.
                        }
                        catch (Exception exc)
                        {
                            logger.LogError($"Unable to send remove player ({character?.Name}) from session ({sessionOwner?.UserName}): " + exc);
                        }
                        continue;
                    }

                    var user = gameData.GetUser(character.UserId);
                    if (user == null)
                    {
                        user = CreateUserFromCharacterData(sessionToken, character, data.Platform, data.PlatformUserId, data.PlatformUserName);
                    }


                    // todo: fix unknowns, we have the data.Platform, data.PlatformUserId, data.PlatformUserName
                    if (!character.Name.Equals(user.UserName, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(data.PlatformUserName))
                    {
                        // ...
                        logger.LogError($"User name mismatch. Character: {character.Name}, User: {user.UserName}. Attempting to fix from data.");

                        //var platform = data.Platform ?? "twitch";
                        //var username = Utility.SanitizeUserName(data.PlatformUserName);
                        //if (!string.IsNullOrEmpty(username))
                        //{
                        //    character.Name = username;
                        //    user.UserName = username;
                        //}
                        //if (!string.IsNullOrEmpty(data.PlatformUserId))
                        //{
                        //    var access = gameData.GetUserAccess(user.Id, data.Platform);
                        //    if (access == null)
                        //    {
                        //        gameData.Add(new UserAccess
                        //        {
                        //            Id = Guid.NewGuid(),
                        //            UserId = user.Id,
                        //            Platform = platform,
                        //            PlatformId = data.PlatformUserId, // needs to be resolved.
                        //            PlatformUsername = username,
                        //            Created = DateTime.UtcNow
                        //        });
                        //    }
                        //    else
                        //    {
                        //        access.PlatformId = data.PlatformUserId;
                        //        access.PlatformUsername = data.PlatformUserName;
                        //    }
                        //}
                    }

                    var characterSessionState = gameData.GetCharacterSessionState(sessionToken.SessionId, character.Id);
                    characterSessionState.LastStateSaveRequest = DateTime.UtcNow;
                    characterSessionState.LastStateUpdate = DateTime.UtcNow;

                    DataModels.CharacterState characterState = null;
                    if (character.StateId != Guid.Empty)
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
                        SetCharacterState(character, state, data);
                    }

                    TrySendToExtension(character, data);
                }
                catch (Exception e)
                {
                    logger.LogError($"Unable to update player state: {e}");
                }
            }
        }
        public void OnExperienceDelta(SessionToken sessionToken, IReadOnlyList<DeltaExperienceUpdate> deltas)
        {
            if (sessionToken == null || deltas == null || deltas.Count == 0)
            {
                return;
            }

            var sessionOwner = gameData.GetUser(sessionToken.UserId);
            if (sessionOwner == null)
            {
                logger.LogError("Unable to find session owner for " + sessionToken.UserName);
                return;
            }

            if (sessionOwner.Status >= 1)
            {
                logger.LogError("The user session from " + sessionOwner.UserName + " trying to save players, but the owner has been banned.");
                return;
            }

            var gameSession = gameData.GetSession(sessionToken.SessionId);
            if (gameSession == null)
            {
                gameSession = GameData.CreateSession(sessionToken.UserId, sessionToken.SessionId);
                gameData.Add(gameSession);
            }

            if (sessionOwner.Status >= 1)
            {
                logger.LogError("The user session from " + sessionOwner.UserName + " trying to save player exp, but the owner has been banned.");
                return;
            }

            var sessionState = gameData.GetSessionState(gameSession.Id);
            if (sessionState != null)
            {
                if (string.IsNullOrEmpty(sessionState.ClientVersion))
                    sessionState.ClientVersion = sessionToken.ClientVersion;

                sessionState.LastExpRequest = DateTime.UtcNow;
            }

            foreach (var delta in deltas)
            {
                var character = gameData.GetCharacter(delta.CharacterId);
                if (character == null)
                {
                    logger.LogError("Trying to update a character that does not exist. " + delta.CharacterId);
                    character = CreateMissingCharacter(delta.CharacterId, gameSession);
                    //SendRemovePlayerFromSession(delta.CharacterId, gameSession, "Character with ID " + delta.CharacterId + " does not exist.");
                    //continue;
                }

                if (delta.DirtyMask == 0 || delta.Changes == null || delta.Changes.Length == 0)
                {
                    logger.LogWarning("Experience delta has no changes. For character " + character.Name + " on " + sessionOwner.UserName);
                    continue;
                }

                // take ownership of the character if its not part of a stream
                if (character.UserIdLock == null)
                {
                    character.UserIdLock = sessionOwner.Id;
                }

                if (character.UserIdLock != sessionOwner.Id)
                {
                    try
                    {
                        if (TryMoveToSession(character, sessionOwner))
                        {
                            continue;
                        }

                        var characterSessionOwner = character.UserIdLock != null ? gameData.GetUser(character.UserIdLock.GetValueOrDefault()) : null;
                        var partOfSession = characterSessionOwner != null ? characterSessionOwner.UserName : "";
                        // check if we had a recent update from the other session owner. If so, we should remove it from this session, otherwise we will keep the player in this session.
                        //logger.LogError("User session lock mismatch. Character: " + character.Name + " Session: " + sessionOwner.UserName + " UserIdLock: " + partOfSession);
                        logger.LogError(character.Name + " is getting exp updates from '" + sessionOwner.UserName + "' but is actually part of '" + partOfSession + "'. Update has been ignored.");
                        SendRemovePlayerFromSession(character, gameSession, "Character is part of another session.");
                        continue;
                    }
                    catch (Exception exc)
                    {
                        logger.LogError($"Unable to send remove player ({character?.Name}) from session ({sessionOwner?.UserName}): " + exc);
                        continue;
                    }
                }


                var user = gameData.GetUser(character.UserId);
                if (user == null)
                {
                    user = CreateUserFromCharacterData(sessionToken, character);
                }

                Skills skills = GetOrCreateCharacterSkills(character);

                var characterSessionState = gameData.GetCharacterSessionState(sessionToken.SessionId, character.Id);
                characterSessionState.LastExpSaveRequest = DateTime.UtcNow;

                foreach (var update in delta.Changes)
                {
                    var level = update.Level;
                    if (level > GameMath.MaxLevel)
                    {
                        continue;
                    }

                    if (level < 0)
                    {
                        level = 1;
                    }

                    var skill = skills.GetSkill(update.Index);
                    if (skill == null) continue;

                    double experience = update.Experience;
                    var existingLevel = skill.Level;

                    if (skill.Level == GameMath.MaxLevel)
                    {
                        var maxExp = GameMath.ExperienceForLevel(GameMath.MaxLevel + 1);
                        if (experience >= maxExp)
                        {
                            experience = maxExp;
                        }
                    }

                    if (skills.Set(skill.Index, level, experience))
                    {
                        if (existingLevel != level)
                        {
                            UpdateCharacterSkillRecord(character.Id, skill.Index, level, experience);
                        }

                        characterSessionState.LastExpUpdate = DateTime.UtcNow;

                        TrySendToExtension(character, new CharacterExpUpdate
                        {
                            CharacterId = character.Id,
                            Experience = experience,
                            Level = level,
                            SkillIndex = skill.Index,
                            Percent = SkillsExtended.GetPercentForNextLevel(skill.Level, skill.Experience),
                        });
                    }
                }
            }
        }

        private bool TryMoveToSession(Character character, User newSessionOwner)
        {
            var characterSessionOwner = character.UserIdLock != null ? gameData.GetUser(character.UserIdLock.GetValueOrDefault()) : null;
            var partOfSession = characterSessionOwner != null ? characterSessionOwner.UserName : "";
            DateTime lastSave = DateTime.UtcNow;
            var ownerSession = gameData.GetSessionByUserId(characterSessionOwner.Id, false, false);
            if (ownerSession != null)
            {
                var ownerSessionState = gameData.GetCharacterSessionState(ownerSession.Id, character.Id);
                lastSave = ownerSessionState.LastExpSaveRequest;
                if (ownerSessionState.LastStateSaveRequest > lastSave)
                {
                    lastSave = ownerSessionState.LastStateSaveRequest;
                }
            }

            var elapsedSinceLastValidUpdate = DateTime.UtcNow - lastSave;
            if (elapsedSinceLastValidUpdate > TimeSpan.FromMinutes(5))
            {
                logger.LogWarning(character.Name + " is getting updates from '" + newSessionOwner.UserName + "' but is actually part of '" + partOfSession + "' and been inactive in owned session for more than " + elapsedSinceLastValidUpdate.TotalMinutes + " minutes. Changing session.");
                character.PrevUserIdLock = character.UserIdLock;
                character.UserIdLock = newSessionOwner.Id;
                SendRemovePlayerFromSession(character, ownerSession, "Character is part of another session.");
                return true;
            }

            return false;
        }

        private Skills GetOrCreateCharacterSkills(Character character)
        {
            var skills = gameData.GetCharacterSkills(character.SkillsId);
            if (skills == null)
            {
                skills = gameData.GenerateSkills();
                character.SkillsId = skills.Id;
                gameData.Add(skills);
            }

            return skills;
        }

        #region Obsolete

        [Obsolete("This uses TCP API which is deprecated for saving, see OnExperienceDelta for new method using the Delta API. This is only kept for backward compatibility")]
        internal void SaveExperience(SessionToken sessionToken, SaveExperienceRequest saveExp)
        {
            try
            {
                if (sessionToken == null || saveExp == null || saveExp.ExpUpdates == null)
                {
                    return;
                }

                var gameSession = gameData.GetSession(sessionToken.SessionId);
                if (gameSession == null)
                {
                    //logger.LogError("Save Exp Request received from an invalid game session. " + sessionToken.UserName);
                    //return;
                    gameSession = GameData.CreateSession(sessionToken.UserId, sessionToken.SessionId);
                    gameData.Add(gameSession);
                }

                var sessionState = gameData.GetSessionState(gameSession.Id);
                if (sessionState != null)
                {
                    if (string.IsNullOrEmpty(sessionState.ClientVersion))
                        sessionState.ClientVersion = sessionToken.ClientVersion;

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
                if (sessionOwner == null)
                {
                    sessionOwner = CreateUserFromSessionData(sessionToken, gameSession);
                }
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
                        character = CreateMissingCharacter(data.CharacterId, gameSession);
                        logger.LogError("Trying to update a character that does not exist. Creating temporary character and user (" + character.Name + ") ID: " + data.CharacterId);
                        //SendRemovePlayerFromSession(data.CharacterId, gameSession, "Character with ID " + data.CharacterId + " does not exist.");
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
                        try
                        {
                            var characterSessionOwner = character.UserIdLock != null ? gameData.GetUser(character.UserIdLock.GetValueOrDefault()) : null;
                            var partOfSession = characterSessionOwner != null ? characterSessionOwner.UserName : "";

                            logger.LogError("Trying to update a character that does not belong to the session owner. Current Session: " + sessionOwner.UserName + " Character: " + character.Name + ", UserIdLock: " + partOfSession);

                            // send remove from this session.
                            SendRemovePlayerFromSession(character, gameSession, "Character is part of another session.");
                        }
                        catch (Exception exc)
                        {
                            logger.LogError($"Unable to send remove player ({character?.Name}) from session ({sessionOwner?.UserName}): " + exc);
                        }
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
                    if (user == null)
                    {
                        user = CreateUserFromCharacterData(sessionToken, character);
                    }

                    foreach (var update in data.Skills)
                    {
                        if (update.Level > GameMath.MaxLevel)
                        {
                            continue;
                        }

                        if (update.Level < 0)
                        {
                            update.Level = 1;
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

                        if (skills.Set(skill.Index, level, experience))
                        {
                            if (existingLevel != level)
                            {
                                UpdateCharacterSkillRecord(character.Id, skill.Index, level, experience);
                            }

                            characterSessionState.LastExpUpdate = DateTime.UtcNow;

                            TrySendToExtension(character, new CharacterExpUpdate
                            {
                                CharacterId = character.Id,
                                Experience = experience,
                                Level = level,
                                SkillIndex = skill.Index,
                                Percent = SkillsExtended.GetPercentForNextLevel(skill.Level, skill.Experience),
                            });
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                logger.LogError(exc, $"Error saving experience in game '{sessionToken.UserName}'. (id={sessionToken.UserId})");
            }
        }

        [Obsolete("Use OnPlayerStateDelta instead. This uses TCP API which is deprecated for saving. This is only kept for backward compatibility")]
        internal void SaveState(SessionToken sessionToken, SaveStateRequest stateUpdate)
        {
            try
            {

                if (sessionToken == null || stateUpdate == null || stateUpdate.StateUpdates == null)
                {
                    return;
                }

                var gameSession = gameData.GetSession(sessionToken.SessionId);
                if (gameSession == null)
                {
                    // its possible that server has crashed. create a game session with same ID for the user.
                    gameSession = GameData.CreateSession(sessionToken.UserId, sessionToken.SessionId);
                    gameData.Add(gameSession);
                    //logger.LogError("Save State Request received from an invalid game session. " + sessionToken.UserName);
                    //return;
                }

                var sessionState = gameData.GetSessionState(gameSession.Id);
                if (sessionState != null)
                {
                    if (string.IsNullOrEmpty(sessionState.ClientVersion))
                        sessionState.ClientVersion = sessionToken.ClientVersion;

                    sessionState.LastStateRequest = DateTime.UtcNow;
                }

                var sessionOwner = gameData.GetUser(gameSession.UserId);
                if (sessionOwner == null)
                {
                    sessionOwner = CreateUserFromSessionData(sessionToken, gameSession);
                    //return;
                }
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
                        character = CreateMissingCharacter(data.CharacterId, gameSession);
                        logger.LogError("Trying to update a character that does not exist. Creating temporary character and user (" + character.Name + ") ID: " + data.CharacterId);
                        //SendRemovePlayerFromSession(data.CharacterId, gameSession, "Character with ID " + data.CharacterId + " does not exist.");
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

                        //logger.LogError("Trying to update a character that does not belong to the session owner. Current Session: " + sessionOwner.UserName + " Character: " + character.Name + ", UserIdLock: " + partOfSession);
                        // send remove from this session.
                        SendRemovePlayerFromSession(character, gameSession, "Character is part of another session.");
                        continue;
                    }

                    var characterSessionState = gameData.GetCharacterSessionState(sessionToken.SessionId, character.Id);
                    characterSessionState.LastStateSaveRequest = DateTime.UtcNow;
                    characterSessionState.LastStateUpdate = DateTime.UtcNow;

                    if (character.StateId != Guid.Empty)
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
                        SetCharacterState(character, state, data);
                    }

                    TrySendToExtension(character, data);
                }
            }
            catch (Exception exc)
            {
                logger.LogError(exc, $"Error saving states in game '{sessionToken.UserName}'. (id={sessionToken.UserId})");
            }
        }

        #endregion

        private User CreateUserFromCharacterData(SessionToken sessionToken, Character character, string? platform = null, string? platformUserId = null, string? platformUserName = null)
        {
            logger.LogError($"The user for character with ID: '{character.Id}' does not exist!. User ID '{character.UserId}'. Attempting to create user. (UserName: {character.Name})");
            var resources = new Resources
            {
                Id = Guid.NewGuid(),
            };
            var username = Utility.SanitizeUserName(platformUserName ?? character.Name);
            var user = new User
            {
                Id = character.UserId,
                Created = DateTime.UtcNow,
                Resources = resources.Id,
                UserName = username,
                DisplayName = username,
            };
            gameData.Add(resources);
            gameData.Add(user);
            gameData.Add(new UserAccess
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Platform = platform ?? "twitch",
                PlatformId = Utility.SanitizeUserName(platformUserId ?? character.Name), // needs to be resolved.
                PlatformUsername = username,
                Created = DateTime.UtcNow
            });
            return user;
        }

        private User CreateUserFromSessionData(SessionToken sessionToken, DataModels.GameSession gameSession)
        {
            User sessionOwner;
            logger.LogError($"The session owner with session id: '{gameSession.Id}' does not exist!. User ID '{gameSession.UserId}'. Attempting to create user.");

            var resources = new Resources
            {
                Id = Guid.NewGuid(),
            };
            var username = Utility.SanitizeUserName(sessionToken.UserName);
            var user = new User
            {
                Id = gameSession.UserId,
                Created = DateTime.UtcNow,
                Resources = resources.Id,
                UserName = username,
                DisplayName = Utility.SanitizeUserName(sessionToken.DisplayName, username),
            };

            gameData.Add(resources);
            gameData.Add(user);

            gameData.Add(new UserAccess
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Platform = "twitch",
                PlatformId = sessionToken.TwitchUserId,
                PlatformUsername = username,
                Created = DateTime.UtcNow
            });
            sessionOwner = user;
            return sessionOwner;
        }

        private static readonly object createMissingCharacterMutex = new object();

        private Character CreateMissingCharacter(CharacterStateDelta characterDelta, DataModels.GameSession gameSession)
        {
            lock (createMissingCharacterMutex)
            {
                var resources = new Resources
                {
                    Id = Guid.NewGuid(),
                };

                var random = new Random();
                var num = random.Next(1000, 10000);

                var username = characterDelta.PlatformUserName ?? ("Unknown" + num);

                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Created = DateTime.UtcNow,
                    Resources = resources.Id,
                    UserName = username,
                    DisplayName = username,
                };

                var userAccess = new UserAccess
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Platform = characterDelta.Platform ?? "twitch",
                    PlatformId = characterDelta.PlatformUserId ?? username,
                    PlatformUsername = username,
                    Created = DateTime.UtcNow
                };

                var syntyAppearance = GenerateRandomSyntyAppearance();
                var skills = gameData.GenerateSkills();
                var statistics = GenerateStatistics();
                var state = new DataModels.CharacterState()
                {
                    Id = Guid.NewGuid(),
                    Health = 10,
                };

                var character = new Character
                {
                    Id = characterDelta.CharacterId,
                    Created = DateTime.UtcNow,
                    LastUsed = DateTime.UtcNow,
                    Name = username,
                    SyntyAppearanceId = syntyAppearance.Id,
                    StateId = state.Id,
                    StatisticsId = statistics.Id,
                    UserId = user.Id,
                    UserIdLock = gameSession.UserId,
                    OriginUserId = gameSession.UserId,
                    SkillsId = skills.Id,
                    CharacterIndex = 0,
                    Identifier = "1"
                };

                gameData.Add(userAccess);
                gameData.Add(user);
                gameData.Add(resources);
                gameData.Add(state);
                gameData.Add(syntyAppearance);
                gameData.Add(statistics);
                gameData.Add(skills);
                gameData.Add(character);

                return character;
            }
        }
        private Character CreateMissingCharacter(Guid characterId, DataModels.GameSession gameSession)
        {
            lock (createMissingCharacterMutex)
            {
                var resources = new Resources
                {
                    Id = Guid.NewGuid(),
                };

                var random = new Random();
                var num = random.Next(1000, 10000);
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Created = DateTime.UtcNow,
                    Resources = resources.Id,
                    UserName = "_missing_user" + num,
                    DisplayName = "_missing_user" + num,
                };

                var syntyAppearance = GenerateRandomSyntyAppearance();
                var skills = gameData.GenerateSkills();
                var statistics = GenerateStatistics();
                var state = new DataModels.CharacterState()
                {
                    Id = Guid.NewGuid(),
                    Health = 10,
                };

                var character = new Character
                {
                    Id = characterId,
                    Created = DateTime.UtcNow,
                    LastUsed = DateTime.UtcNow,
                    Name = "_missing_char" + num,
                    SyntyAppearanceId = syntyAppearance.Id,
                    StateId = state.Id,
                    StatisticsId = statistics.Id,
                    UserId = user.Id,
                    UserIdLock = gameSession.UserId,
                    OriginUserId = gameSession.UserId,
                    SkillsId = skills.Id,
                    CharacterIndex = 0,
                    Identifier = "1"
                };

                gameData.Add(user);
                gameData.Add(resources);
                gameData.Add(state);
                gameData.Add(syntyAppearance);
                gameData.Add(statistics);
                gameData.Add(skills);
                gameData.Add(character);

                return character;
            }
        }

        public async Task UnstuckBrokenPlayersAsync()
        {
            foreach (var c in gameData.GetCharacters())
            {
                var state = gameData.GetCharacterState(c.StateId);
                if (state == null) continue;

                if (!string.IsNullOrEmpty(state.Task))
                    continue;

                // stuck on heim
                if (state.X == -146)
                {
                    state.Island = "Heim";
                    state.X = -285.8875f;
                    state.Y = -2.125;
                    state.Z = -400.75;
                    await UnstuckPlayerAsync(c.Id);
                    await Task.Delay(10);
                }
                else if (!string.IsNullOrEmpty(state.Island)) // stuck not training.
                {
                    state.Task = "Fighting";
                    state.TaskArgument = "all";
                    await SendRejoinAsync(c, "Unstuck used by admin.");
                    await Task.Delay(10);
                }
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

                if (string.IsNullOrEmpty(state.Task))
                {
                    state.Task = "Fighting";
                    state.TaskArgument = "all";
                }

                if (state.Island != null)
                {
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
                            state.X = -285.8875f;
                            state.Y = -2.125;
                            state.Z = -400.75;
                            break;

                        case "Atria":
                            state.X = 480.678f;
                            state.Y = -1.09f;
                            state.Z = -251.281f;
                            break;

                        case "Eldara":
                            state.X = 649.458f;
                            state.Y = -1.45f;
                            state.Z = 37.9f;
                            break;

                        default:
                            state.Y = state.Y + 5f;
                            break;
                    }
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
                    toState.AutoJoinDungeonCounter = fromState.AutoJoinDungeonCounter;
                    toState.AutoJoinRaidCounter = fromState.AutoJoinRaidCounter;
                    toState.IsAutoResting = fromState.IsAutoResting;
                    toState.AutoJoinDungeonCount = fromState.AutoJoinDungeonCount;
                    toState.AutoJoinRaidCount = fromState.AutoJoinRaidCount;
                    toState.AutoTrainTargetLevel = fromState.AutoTrainTargetLevel;
                    toState.AutoRestTarget = fromState.AutoRestTarget;
                    toState.AutoRestStart = fromState.AutoRestStart;
                    toState.DungeonCombatStyle = fromState.DungeonCombatStyle;
                    toState.RaidCombatStyle = fromState.RaidCombatStyle;
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
            if (skillLevel < 1 || skillLevel > GameMath.MaxLevel + 10 || double.IsNaN(skillLevel))
                skillLevel = 1;

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

        internal void SendGameStateToTwitchExtension(SessionToken sessionToken, RavenNest.Models.TcpApi.GameStateRequest gameState)
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

        internal bool TrySendToExtension<T>(Character character, T data)
        {
            if (extensionWsConnectionProvider.TryGet(character.Id, out var connection))
            {
                try
                {
                    connection.EnqueueSend(data);
                    return true;
                }
                catch (Exception exc)
                {
                    logger.LogError("Unable to send extension data to " + character?.Name + " of type " + typeof(T).FullName + ": " + exc);
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
                logger.LogError("Unable to remove player from session, game session is null for character " + character.Name + " (" + character.Id + ") UserId=" + character.UserId + "!");
                return;
            }
            try
            {
                var characterUser = gameData.GetUser(character.UserId);
                if (characterUser == null)
                {
                    // user does not exist?
                    logger.LogError("Unable to remove player from session, user missing for character " + character.Name + " (" + character.Id + ") UserId=" + character.UserId + "!");
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

                var gameEvent = gameData.CreateSessionEvent(GameEventType.PlayerRemove,
                    gameSession,
                    new PlayerRemove()
                    {
                        Reason = clientMessage,
                        UserId = characterUser.Id,
                        CharacterId = character.Id
                    });

                gameData.SendGameEventImmediately(gameEvent);
            }
            catch (Exception exc)
            {
                logger.LogError("Error sending remove player event: " + exc);
            }
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

            string usernamePostfix = null;
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

                // lets check if there is an existing user with the same username.
                // if so, we want to set the current username to @<platform>

                var collidingUser = gameData.GetUserByUsername(playerData.UserName);
                if (collidingUser != null)
                {
                    usernamePostfix = "@" + playerData.Platform.ToLower();
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
                    UserName = playerData.UserName + usernamePostfix,
                    DisplayName = playerData.UserName,
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

            return CreatePlayer(session, user, playerData, usernamePostfix);
        }

        private Task<Player> CreatePlayer(DataModels.GameSession session, User user, PlayerJoinData playerData, string usernamePostfix = null)
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

            logger.LogInformation("Creating new character for '" + user.UserName + "' Data: " + Newtonsoft.Json.JsonConvert.SerializeObject(playerData));

            var username = user.UserName;
            if (!string.IsNullOrEmpty(usernamePostfix))
            {
                username = username.Replace(usernamePostfix, "");
            }

            var character = new Character
            {
                Id = Guid.NewGuid(),
                Name = username,
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
            if (sessionCharacters == null) return null;
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
        private static bool HasFlag(CharacterStateUpdate a, CharacterFlags b) => HasFlag(a.State, b);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasFlag(CharacterFlags a, CharacterFlags b) => (a & b) == b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetCharacterState(DataModels.Character character, DataModels.CharacterState state, CharacterStateUpdate update)
        {
            var task = GetTaskBySkillIndex(update.TrainingSkillIndex);
            var taskArgument = update.TaskArgument;
            if (string.IsNullOrEmpty(taskArgument) || task != "Fighting")
                taskArgument = GetTaskArgumentBySkillIndex(update.TrainingSkillIndex);
            if (string.IsNullOrEmpty(taskArgument))
                taskArgument = update.TaskArgument;

            state.Health = update.Health;
            state.InArena = HasFlag(update, CharacterFlags.InArena);
            state.InRaid = HasFlag(update, CharacterFlags.InRaid);
            state.InDungeon = HasFlag(update, CharacterFlags.InDungeon);
            state.JoinedDungeon = HasFlag(update, CharacterFlags.InDungeonQueue);
            state.InOnsen = HasFlag(update, CharacterFlags.InOnsen);
            state.IsCaptain = HasFlag(update, CharacterFlags.IsCaptain);
            state.Island = update.Island != RavenNest.Models.Island.Ferry ? update.Island.ToString() : null;
            state.Destination = update.Destination != RavenNest.Models.Island.Ferry ? update.Island.ToString() : null;
            state.ExpPerHour = update.ExpPerHour;
            state.EstimatedTimeForLevelUp = update.EstimatedTimeForLevelUp.ToString("yyyy-MM-ddTHH:mm:ss.fffffff");
            state.Task = task;
            state.TaskArgument = taskArgument;
            state.AutoJoinRaidCounter = update.AutoJoinRaidCounter;
            state.AutoJoinDungeonCounter = update.AutoJoinDungeonCounter;

            var coinCost = 0L;
            if (update.AutoJoinRaidCount > state.AutoJoinRaidCount)
            {
                var delta = (update.AutoJoinRaidCount - state.AutoJoinRaidCount);
                coinCost += delta * AutoJoinRaidCost;
            }

            if (update.AutoJoinDungeonCount > state.AutoJoinDungeonCount)
            {
                var delta = (update.AutoJoinDungeonCount - state.AutoJoinDungeonCount);
                coinCost += delta * AutoJoinDungeonCost;
            }

            //if (update.AutoRestCount > state.IsAutoResting)
            //{
            //    var delta = update.AutoRestCount - state.IsAutoResting;
            //    coinCost += delta * AutoRestCost;
            //}

            state.AutoJoinDungeonCount = update.AutoJoinRaidCount;
            state.AutoJoinRaidCount = update.AutoJoinRaidCount;
            state.IsAutoResting = update.IsAutoResting;
            state.X = update.X;
            state.Y = update.Y;
            state.Z = update.Z;

            state.AutoTrainTargetLevel = update.AutoTrainTargetLevel;
            state.AutoRestTarget = update.AutoRestTarget;
            state.AutoRestStart = update.AutoRestStart;
            state.DungeonCombatStyle = update.DungeonCombatStyle;
            state.RaidCombatStyle = update.RaidCombatStyle;

            return;
#warning auto join raids and dungeon costs are 0!!!
            if (coinCost > 0)
            {
                var res = gameData.GetResources(character);
                //var cost = AutoJoinDungeonCost;
                if (coinCost > res.Coins)
                {
                    res.Coins = 0;
                    return;
                }
                res.Coins -= coinCost;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetCharacterState(DataModels.Character character, DataModels.CharacterState state, CharacterStateDelta update)
        {
            // task and task arguments should always be set together.

            if (update.HasValue(CharacterStateFields.TrainingSkill))
            {
                var task = GetTaskBySkillIndex(update.TrainingSkillIndex);
                state.Task = task;
            }

            if (update.HasValue(CharacterStateFields.TaskArgument))
            {
                var taskArgument = update.TaskArgument;
                if (string.IsNullOrEmpty(taskArgument) || state.Task != "Fighting")
                    taskArgument = GetTaskArgumentBySkillIndex(update.TrainingSkillIndex);
                if (string.IsNullOrEmpty(taskArgument))
                    taskArgument = update.TaskArgument;
                state.TaskArgument = taskArgument;
            }

            if (update.HasValue(CharacterStateFields.Health))
            {
                state.Health = update.Health;
            }

            if (update.HasValue(CharacterStateFields.State))
            {
                state.InArena = HasFlag(update.State, CharacterFlags.InArena);
                state.InRaid = HasFlag(update.State, CharacterFlags.InRaid);
                state.InDungeon = HasFlag(update.State, CharacterFlags.InDungeon);
                state.JoinedDungeon = HasFlag(update.State, CharacterFlags.InDungeonQueue);
                state.InOnsen = HasFlag(update.State, CharacterFlags.InOnsen);
                state.IsCaptain = HasFlag(update.State, CharacterFlags.IsCaptain);
            }

            if (update.HasValue(CharacterStateFields.Island))
            {
                state.Island = update.Island != RavenNest.Models.Island.Ferry ? update.Island.ToString() : null;
            }

            if (update.HasValue(CharacterStateFields.Destination))
            {
                state.Destination = update.Destination != RavenNest.Models.Island.Ferry ? update.Island.ToString() : null;
            }

            if (update.HasValue(CharacterStateFields.ExpPerHour))
            {
                state.ExpPerHour = update.ExpPerHour;
            }

            if (update.HasValue(CharacterStateFields.LevelUpETA))
            {
                state.EstimatedTimeForLevelUp = update.EstimatedTimeForLevelUp.ToString("yyyy-MM-ddTHH:mm:ss.fffffff");
            }


            state.AutoJoinRaidCounter = update.AutoJoinRaidCounter;

            var coinCost = 0L;
            if (update.AutoJoinRaidCount > state.AutoJoinRaidCount)
            {
                var delta = (update.AutoJoinRaidCount - state.AutoJoinRaidCount);
                coinCost += delta * AutoJoinRaidCost;
            }

            if (update.AutoJoinDungeonCount > state.AutoJoinDungeonCount)
            {
                var delta = (update.AutoJoinDungeonCount - state.AutoJoinDungeonCount);
                coinCost += delta * AutoJoinDungeonCost;
            }

            //if (update.AutoRestCount > state.IsAutoResting)
            //{
            //    var delta = update.AutoRestCount - state.IsAutoResting;
            //    coinCost += delta * AutoRestCost;
            //}

            if (update.HasValue(CharacterStateFields.AutoJoinDungeon))
            {
                state.AutoJoinDungeonCount = update.AutoJoinDungeonCount;
                state.AutoJoinDungeonCounter = update.AutoJoinDungeonCounter;
            }

            if (update.HasValue(CharacterStateFields.AutoJoinRaid))
            {
                state.AutoJoinRaidCount = update.AutoJoinRaidCount;
                state.AutoJoinRaidCounter = update.AutoJoinRaidCounter;
            }

            if (update.HasValue(CharacterStateFields.IsAutoResting))
            {
                state.IsAutoResting = update.IsAutoResting;
            }

            if (update.HasValue(CharacterStateFields.Position))
            {
                state.X = update.X;
                state.Y = update.Y;
                state.Z = update.Z;
            }

            if (update.HasValue(CharacterStateFields.AutoTrainLevel))
            {
                state.AutoTrainTargetLevel = update.AutoTrainTargetLevel;
            }

            if (update.HasValue(CharacterStateFields.AutoRestTarget))
            {
                state.AutoRestTarget = update.AutoRestTarget;
            }

            if (update.HasValue(CharacterStateFields.AutoRestTarget))
            {
                state.AutoRestTarget = update.AutoRestTarget;
            }

            if (update.HasValue(CharacterStateFields.AutoRestStart))
            {
                state.AutoRestStart = update.AutoRestStart;
            }

            if (update.HasValue(CharacterStateFields.DungeonStyle))
            {
                state.DungeonCombatStyle = update.DungeonCombatStyle;
            }

            if (update.HasValue(CharacterStateFields.RaidStyle))
            {
                state.RaidCombatStyle = update.RaidCombatStyle;
            }

            return;
#warning auto join raids and dungeon costs are 0!!!
            if (coinCost > 0)
            {
                var res = gameData.GetResources(character);
                //var cost = AutoJoinDungeonCost;
                if (coinCost > res.Coins)
                {
                    res.Coins = 0;
                    return;
                }
                res.Coins -= coinCost;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private DataModels.CharacterState CreateCharacterState(CharacterStateDelta update)
        {

            // when creating a character state, we have to assume we have the full state,
            // this should only happen if server was restarted  before a previous character state was properly saved.
            // so when the client gets connected again, it should send the full state.

            var state = new DataModels.CharacterState
            {
                Id = Guid.NewGuid()
            };

            state.Task = GetTaskBySkillIndex(update.TrainingSkillIndex);
            var taskArgument = update.TaskArgument;
            if (string.IsNullOrEmpty(taskArgument) || state.Task != "Fighting")
                taskArgument = GetTaskArgumentBySkillIndex(update.TrainingSkillIndex);
            state.TaskArgument = taskArgument;

            state.RestedTime = 0;
            state.Island = update.Island != RavenNest.Models.Island.Ferry ? update.Island.ToString() : null;
            state.Destination = update.Destination != RavenNest.Models.Island.Ferry ? update.Island.ToString() : null;
            state.ExpPerHour = update.ExpPerHour;
            state.EstimatedTimeForLevelUp = update.EstimatedTimeForLevelUp.ToString("yyyy-MM-ddTHH:mm:ss.fffffff");
            state.AutoJoinRaidCounter = update.AutoJoinRaidCounter;
            state.AutoJoinDungeonCounter = update.AutoJoinDungeonCounter;
            state.AutoJoinDungeonCount = update.AutoJoinDungeonCount;
            state.AutoJoinRaidCount = update.AutoJoinRaidCount;
            state.IsAutoResting = update.IsAutoResting;
            state.AutoRestStart = update.AutoRestStart;
            state.AutoRestTarget = update.AutoRestTarget;
            state.AutoTrainTargetLevel = update.AutoTrainTargetLevel;
            state.RaidCombatStyle = update.RaidCombatStyle;
            state.DungeonCombatStyle = update.DungeonCombatStyle;

            state.X = update.X;
            state.Y = update.Y;
            state.Z = update.Z;

            state.Health = update.Health;
            state.InArena = HasFlag(update.State, CharacterFlags.InArena);
            state.InRaid = HasFlag(update.State, CharacterFlags.InRaid);
            state.InDungeon = HasFlag(update.State, CharacterFlags.InDungeon);
            state.InOnsen = HasFlag(update.State, CharacterFlags.InOnsen);
            state.JoinedDungeon = HasFlag(update.State, CharacterFlags.InDungeonQueue);
            state.IsCaptain = HasFlag(update.State, CharacterFlags.IsCaptain);
            state.InArena = HasFlag(update.State, CharacterFlags.InArena);

            return state;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static DataModels.CharacterState CreateCharacterState(CharacterStateUpdate update)
        {
            var task = GetTaskBySkillIndex(update.TrainingSkillIndex);
            var taskArgument = update.TaskArgument;
            if (string.IsNullOrEmpty(taskArgument) || task != "Fighting")
                taskArgument = GetTaskArgumentBySkillIndex(update.TrainingSkillIndex);

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
                Island = update.Island != RavenNest.Models.Island.Ferry ? update.Island.ToString() : null,
                Destination = update.Destination != RavenNest.Models.Island.Ferry ? update.Island.ToString() : null,
                ExpPerHour = update.ExpPerHour,
                EstimatedTimeForLevelUp = update.EstimatedTimeForLevelUp.ToString("yyyy-MM-ddTHH:mm:ss.fffffff"),
                AutoJoinRaidCounter = update.AutoJoinRaidCounter,
                AutoJoinDungeonCounter = update.AutoJoinDungeonCounter,
                AutoJoinDungeonCount = update.AutoJoinDungeonCount,
                IsAutoResting = update.IsAutoResting,
                AutoJoinRaidCount = update.AutoJoinRaidCount,
                AutoRestStart = update.AutoRestStart,
                AutoRestTarget = update.AutoRestTarget,
                AutoTrainTargetLevel = update.AutoTrainTargetLevel,
                RaidCombatStyle = update.RaidCombatStyle,
                DungeonCombatStyle = update.DungeonCombatStyle,
                RestedTime = 0,
                Task = task,
                TaskArgument = taskArgument ?? task,
                X = update.X,
                Y = update.Y,
                Z = update.Z,
            };
            return state;
        }

        private void UpdateResources(DataModels.GameSession session, Character character, DataModels.Resources resources)
        {
            //var user = gameData.GetUser(character.UserId);
            var gameEvent = gameData.CreateSessionEvent(GameEventType.ResourceUpdate, session,
                new ResourceUpdate
                {
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

        public async Task DeletePlayer(Guid characterId)
        {
            var c = gameData.GetCharacter(characterId);
            if (c != null)
            {
                gameData.RemoveCharacter(c);
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

        public async Task<bool> PlayerRemoveFailed(SessionToken sessionToken, Guid characterId, string reason)
        {
            // just log it for now.
            var character = gameData.GetCharacter(characterId);
            var session = gameData.GetSession(sessionToken.SessionId);
            var sessionName = sessionToken.UserName;

            if (character == null || session == null)
            {
                logger.LogError($"PlayerRemoveFailed: {characterId} @ {sessionName}, reason: {reason}");
                return true;
            }

            var characterName = character.Name;
            logger.LogError($"PlayerRemoveFailed: {characterId} ({characterName}) @ {sessionName}, reason: {reason}");
            return true;
        }
    }
}
