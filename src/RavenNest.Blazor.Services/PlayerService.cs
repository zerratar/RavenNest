using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Game;
using RavenNest.DataModels;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class PlayerService : RavenNestService
    {
        private readonly GameData gameData;
        private readonly PlayerManager playerManager;
        private readonly AdminManager adminManager;

        public PlayerService(
            GameData gameData,
            PlayerManager playerManager,
            AdminManager adminManager,
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.playerManager = playerManager;
            this.adminManager = adminManager;
        }

        public void MakeNameMatchUsername(WebsitePlayer player)
        {
            player.Name = player.UserName;
            var character = gameData.GetCharacter(player.Id);
            character.Name = player.UserName;
        }

        public void SetActiveCharacter(WebsitePlayer player)
        {
            var session = GetSession();
            sessionInfoProvider.SetActiveCharacter(session, player.Id);
        }
        public void SetActiveCharacter(WebsiteAdminPlayer player)
        {
            var session = GetSession();
            sessionInfoProvider.SetActiveCharacter(session, player.Id);
        }

        public void Kick(WebsiteAdminPlayer player)
        {
            adminManager.KickPlayer(player.Id);
            player.SessionInfo = null;
            player.SessionName = null;
        }

        public void AddToMySession(WebsiteAdminPlayer player)
        {
            var session = GetSession();
            var gameSession = gameData.GetSessionByUserId(session.UserId);
            if (gameSession != null)
            {
                var character = gameData.GetCharacter(player.Id);
                var owner = gameData.GetUser(gameSession.UserId);
                playerManager.SendPlayerAddToSession(character, gameSession);

                player.SessionInfo = new CharacterSessionInfo
                {
                    OwnerDisplayName = owner.DisplayName,
                    OwnerUserName = owner.UserName,
                };

                player.SessionName = owner.UserName;
            }
        }

        public Task<bool> UpdatePlayerSkillAsync(Guid characterId, string skillName, int level, float levelProgress)
        {
            return playerManager.UpdatePlayerSkillAsync(characterId, skillName, level, levelProgress);
        }

        public void UpdatePlayerIdentifier(Guid characterId, string identifier)
        {
            var c = gameData.GetCharacter(characterId);
            if (c != null)
            {
                c.Identifier = identifier;
            }
        }

        public int GetCombatLevel(WebsitePlayer player)
        {
            return (int)(((player.Skills.AttackLevel + player.Skills.DefenseLevel + player.Skills.HealthLevel + player.Skills.StrengthLevel) / 4f) +
                   ((player.Skills.RangedLevel + player.Skills.MagicLevel + player.Skills.HealingLevel) / 8f));
        }

        public int GetCombatLevel(Player player)
        {
            return (int)(((player.Skills.AttackLevel + player.Skills.DefenseLevel + player.Skills.HealthLevel + player.Skills.StrengthLevel) / 4f) +
                   ((player.Skills.RangedLevel + player.Skills.MagicLevel + player.Skills.HealingLevel) / 8f));
        }

        public bool SendToCharacter(Guid characterId, RavenNest.Models.UserBankItem item)
        {
            return playerManager.SendToCharacter(characterId, item, item.Amount);
        }

        public WebsitePlayer SendToCharacter(Guid characterId, Guid otherCharacterId, RavenNest.Models.InventoryItem item, long amount)
        {
            playerManager.SendToCharacter(characterId, otherCharacterId, item, amount);
            return playerManager.GetWebsitePlayer(characterId);
        }

        public WebsitePlayer Vendor(Guid characterId, RavenNest.Models.InventoryItem item, long amount)
        {
            playerManager.VendorItem(characterId, item, amount);
            return playerManager.GetWebsitePlayer(characterId);
        }

        public WebsitePlayer SendToStash(Guid characterId, RavenNest.Models.InventoryItem item, long amount)
        {
            playerManager.SendToStash(characterId, item, amount);
            return playerManager.GetWebsitePlayer(characterId);
        }

        public WebsitePlayer UnequipItem(Guid characterId, RavenNest.Models.InventoryItem item)
        {
            playerManager.UnequipItem(characterId, item);
            return playerManager.GetWebsitePlayer(characterId);
        }

        public WebsitePlayer EquipItem(Guid characterId, RavenNest.Models.InventoryItem item)
        {
            playerManager.EquipItem(characterId, item);
            return playerManager.GetWebsitePlayer(characterId);
        }

        public WebsitePlayer AddItem(Guid characterId, RavenNest.Models.Item item)
        {
            playerManager.AddItem(characterId, item.Id);
            return playerManager.GetWebsitePlayer(characterId);
        }

        public Task<bool> ResetPlayerSkillsAsync(Guid characterId)
        {
            return playerManager.ResetPlayerSkillsAsync(characterId);
        }

        public async Task<bool> UnstuckPlayerAsync(Guid fromCharacterId)
        {
            var session = GetSession();
            if (session == null) return false;
            return await playerManager.UnstuckPlayerAsync(fromCharacterId);
        }

        public async Task<bool> CloneSkillsAndStateToMainAsync(Guid fromCharacterId)
        {
            var session = GetSession();
            if (session == null) return false;
            var mainCharacter = gameData.GetCharacterByUserId(session.UserId);
            if (mainCharacter == null) return false;
            return await playerManager.CloneSkillsAndStateAsync(fromCharacterId, mainCharacter.Id);
        }

        public async Task<WebsitePlayer> GetPlayerAsync(Guid characterId)
        {
            return await Task.Run(() =>
            {
                return playerManager.GetWebsitePlayer(characterId);
            });
        }
        public async Task<WebsitePlayer> GetMyPlayerByIndexAsync(int index)
        {
            return await Task.Run(() =>
            {
                var session = GetSession();
                var userId = session.UserId;
                return playerManager.GetWebsitePlayer(userId, index.ToString());
            });
        }

        public async Task<IReadOnlyList<WebsitePlayer>> GetMyPlayersAsync()
        {
            return await Task.Run(() =>
            {
                var session = GetSession();
                var userId = session.UserId;
                return playerManager.GetWebsitePlayers(userId);
            });
        }

        public async Task<PagedCollection<WebsiteAdminPlayer>> GetPlayerPageAsync(string search, int pageIndex, int pageSize)
        {
            var players = await SearchForWebsiteAdminPlayersAsync(search, false, true);
            var pageItems = players.Skip(pageSize * pageIndex).Take(pageSize).ToList();
            return new PagedCollection<WebsiteAdminPlayer>(pageItems, players.Count);
        }

        public Task<IReadOnlyList<Player>> SearchForPlayersAsync(string searchText)
        {
            return SearchForPlayersAsync(searchText, false);
        }
        public async Task<IReadOnlyList<Player>> SearchForPlayersAsync(string searchText, bool ignoreClanInvitedPlayers = true, bool allOnEmptySearch = false)
        {
            return await Task.Run(() =>
            {
                IEnumerable<Player> players = playerManager.GetPlayers();
                if (ignoreClanInvitedPlayers)
                {
                    var session = GetSession();
                    var user = gameData.GetUser(session.UserId);
                    if (user == null)
                        return new List<Player>();

                    var clan = gameData.GetClanByOwner(user.Id);
                    players = players.Where(x => x.Clan == null &&
                        gameData.GetClanInvitesByCharacter(x.Id).All(y => y.ClanId != clan.Id));
                }

                if (string.IsNullOrEmpty(searchText))
                {
                    if (allOnEmptySearch)
                        return players.AsList();

                    return new List<Player>();
                }

                var list = new List<Player>();
                foreach (var player in players)
                {
                    if (player.UserName.Contains(searchText, System.StringComparison.OrdinalIgnoreCase) || player.Name.Contains(searchText, System.StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(player);
                    }
                    else
                    {
                        foreach (var access in gameData.GetUserAccess(player.UserId))
                        {
                            // this could lead to wrong account being mapped to a specific player if two different accounts cross platforms have the same username or id.
                            if (access.PlatformId.Contains(searchText, System.StringComparison.OrdinalIgnoreCase) || access.PlatformUsername.Contains(searchText, System.StringComparison.OrdinalIgnoreCase))
                            {
                                list.Add(player);
                                break;
                            }
                        }
                    }
                }
                return list;
            });
        }

        public async Task<IReadOnlyList<WebsiteAdminPlayer>> SearchForWebsiteAdminPlayersAsync(string searchText, bool ignoreClanInvitedPlayers = true, bool allOnEmptySearch = false)
        {
            return await Task.Run(() =>
            {
                var empty = (IReadOnlyList<WebsiteAdminPlayer>)Array.Empty<WebsiteAdminPlayer>();
                IEnumerable<WebsiteAdminPlayer> players = playerManager.GetWebsiteAdminPlayers();
                if (ignoreClanInvitedPlayers)
                {
                    var session = GetSession();
                    var user = gameData.GetUser(session.UserId);
                    if (user == null)
                        return empty;

                    var clan = gameData.GetClanByOwner(user.Id);
                    players = players.Where(x => x.Clan == null &&
                        gameData.GetClanInvitesByCharacter(x.Id).All(y => y.ClanId != clan.Id));
                }

                if (string.IsNullOrEmpty(searchText))
                {
                    if (allOnEmptySearch)
                        return players.AsList();

                    return empty;
                }

                var list = new List<WebsiteAdminPlayer>();
                foreach (var player in players)
                {
                    if (player.UserName.Contains(searchText, System.StringComparison.OrdinalIgnoreCase) || player.Name.Contains(searchText, System.StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(player);
                    }
                    else
                    {
                        foreach (var access in gameData.GetUserAccess(player.UserId))
                        {
                            // this could lead to wrong account being mapped to a specific player if two different accounts cross platforms have the same username or id.
                            if (access.PlatformId.Contains(searchText, System.StringComparison.OrdinalIgnoreCase) || access.PlatformUsername.Contains(searchText, System.StringComparison.OrdinalIgnoreCase))
                            {
                                list.Add(player);
                                break;
                            }
                        }
                    }
                }
                return list;
            });
        }
    }
}
