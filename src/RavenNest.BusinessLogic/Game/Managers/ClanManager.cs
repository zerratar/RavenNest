using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.BusinessLogic.Game
{
    public class ClanManager : IClanManager
    {
        private readonly IGameData gameData;
        private readonly INotificationManager notificationManager;

        public ClanManager(
            IGameData gameData,
            INotificationManager notificationManager)
        {
            this.gameData = gameData;
            this.notificationManager = notificationManager;
        }

        public bool RemovePlayerInvite(Guid inviteId)
        {
            var invite = gameData.GetClanInvite(inviteId);
            if (invite == null)
                return false;

            if (invite.NotificationId != null)
            {
                var notification = gameData.GetNotification(invite.NotificationId.GetValueOrDefault());
                if (notification != null)
                {
                    gameData.Remove(notification);
                }
            }

            gameData.Remove(invite);
            return true;
        }

        public bool RemovePlayerInvite(Guid clanId, Guid characterId)
        {
            // character does not exist
            var character = gameData.GetCharacter(characterId);
            if (character == null)
                return false;

            // clan does not exist
            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return false;

            // no invite available
            var invite = gameData.GetClanInvitesByCharacter(characterId).FirstOrDefault(x => x.ClanId == clanId);
            if (invite == null)
                return false;

            if (invite.NotificationId != null)
            {
                var notification = gameData.GetNotification(invite.NotificationId.GetValueOrDefault());
                if (notification != null)
                {
                    gameData.Remove(notification);
                }
            }

            gameData.Remove(invite);
            return true;
        }

        public bool SendPlayerInvite(Guid clanId, Guid characterId, Guid? senderUserId = null)
        {
            // character does not exist
            var character = gameData.GetCharacter(characterId);
            if (character == null)
                return false;

            // clan does not exist
            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return false;

            // existing invite to same clan.
            var invite = gameData.GetClanInvitesByCharacter(characterId).FirstOrDefault(x => x.ClanId == clanId);
            if (invite != null)
                return false;

            invite = new DataModels.CharacterClanInvite
            {
                Id = Guid.NewGuid(),
                CharacterId = characterId,
                ClanId = clanId,
                Created = DateTime.UtcNow,
                InviterUserId = senderUserId
            };
            invite.NotificationId = notificationManager.ClanInviteReceived(clanId, characterId, senderUserId)?.Id;
            gameData.Add(invite);
            return true;
        }

        public bool AcceptClanInvite(Guid inviteId)
        {
            // invite does not exist
            var invite = gameData.GetClanInvite(inviteId);
            if (invite == null)
                return false;

            // character does not exist
            var character = gameData.GetCharacter(invite.CharacterId);
            if (character == null)
                return false;

            var membership = gameData.GetClanMembership(invite.CharacterId);
            if (membership != null)
                return false;

            // clan does not exist
            var clan = gameData.GetClan(invite.ClanId);
            if (clan == null)
                return false;

            var roles = gameData.GetClanRoles(clan.Id);
            var role = roles.OrderBy(x => x.Level).FirstOrDefault(x => x.Level > 0);
            if (role == null)
                role = roles.FirstOrDefault();

            var appearance = gameData.GetAppearance(character.SyntyAppearanceId);
            if (appearance == null)
                return false;

            appearance.Cape = 0;

            if (role == null)
            {
                CreateDefaultRoles(clan);
                role = gameData.GetClanRoles(clan.Id).OrderBy(x => x.Level).FirstOrDefault(x => x.Level > 0);
            }

            gameData.Add(new DataModels.CharacterClanMembership
            {
                Id = Guid.NewGuid(),
                ClanId = clan.Id,
                CharacterId = character.Id,
                ClanRoleId = role.Id,
                Joined = DateTime.UtcNow,
            });
            gameData.Remove(invite);

            notificationManager.ClanInviteAccepted(invite.ClanId, invite.CharacterId, DateTime.UtcNow, invite.InviterUserId);
            return true;
        }

        public bool RemoveClanInvite(Guid inviteId)
        {
            // invite does not exist
            var invite = gameData.GetClanInvite(inviteId);
            if (invite == null)
                return false;

            gameData.Remove(invite);
            return true;
        }

        public Clan GetClanByUserId(string userId)
        {
            var user = gameData.GetUser(userId);
            if (user == null)
                return null;
            var clan = gameData.GetClanByUser(user.Id);
            if (clan == null)
                return null;
            return ModelMapper.Map(gameData, clan);
        }

        public Clan GetClanByCharacter(Guid characterId)
        {
            var clanMembership = gameData.GetClanMembership(characterId);
            if (clanMembership == null) return null;
            var clan = gameData.GetClan(clanMembership.ClanId);
            return ModelMapper.Map(gameData, clan);
        }

        public Clan GetClan(Guid clanId)
        {
            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return null;
            return ModelMapper.Map(gameData, clan);
        }

        public bool AddClanMember(Guid clanId, Guid characterId, Guid roleId)
        {
            // clan does not exist
            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return false;

            // character does not exist
            var character = gameData.GetCharacter(characterId);
            if (character == null)
                return false;

            // character already a member of a clan
            var membership = gameData.GetClanMembership(characterId);
            if (membership != null)
                return false;

            // No such role
            var role = gameData.GetClanRole(roleId);
            if (role == null)
                return false;

            gameData.Add(new DataModels.CharacterClanMembership
            {
                CharacterId = characterId,
                ClanId = clanId,
                ClanRoleId = roleId,
                Id = Guid.NewGuid(),
                Joined = DateTime.UtcNow
            });
            return true;
        }

        public bool AssignMemberClanRole(Guid clanId, Guid characterId, Guid roleId)
        {
            // clan does not exist
            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return false;

            // character does not exist
            var character = gameData.GetCharacter(characterId);
            if (character == null)
                return false;

            // character already a member of a clan
            var membership = gameData.GetClanMembership(characterId);
            if (membership == null)
                return false;

            // current membership not part of same clan
            if (membership.ClanId != clanId)
                return false;

            // No such role
            var role = gameData.GetClanRole(roleId);
            if (role == null)
                return false;

            membership.ClanRoleId = role.Id;
            return true;
        }

        public Clan CreateClan(string userId, string name, string logo)
        {
            var user = gameData.GetUser(userId);
            if (user == null)
                return null;

            return CreateClan(user.Id, name, logo);
        }

        public Clan CreateClan(Guid ownerUserId, string name, string logoImageFile)
        {
            // already have a clan
            var clan = gameData.GetClanByUser(ownerUserId);
            if (clan != null)
                return null;

            // no such user
            var user = gameData.GetUser(ownerUserId);
            if (user == null)
                return null;

            clan = new DataModels.Clan()
            {
                Id = Guid.NewGuid(),
                Logo = logoImageFile,
                Name = name,
                UserId = ownerUserId,
                Created = DateTime.UtcNow
            };
            gameData.Add(clan);

            CreateDefaultRoles(clan);

            return ModelMapper.Map(gameData, clan);
        }

        private void CreateDefaultRoles(DataModels.Clan clan)
        {
            gameData.Add(new DataModels.ClanRole
            {
                ClanId = clan.Id,
                Id = Guid.NewGuid(),
                Level = 3,
                Name = "Officer",
                Cape = 0,
            });
            gameData.Add(new DataModels.ClanRole
            {
                ClanId = clan.Id,
                Id = Guid.NewGuid(),
                Level = 2,
                Name = "Member",
                Cape = 0,
            });
            gameData.Add(new DataModels.ClanRole
            {
                ClanId = clan.Id,
                Id = Guid.NewGuid(),
                Level = 1,
                Name = "Recruit",
                Cape = 0,
            });
            gameData.Add(new DataModels.ClanRole
            {
                ClanId = clan.Id,
                Id = Guid.NewGuid(),
                Level = 0,
                Name = "Inactive",
                Cape = 0,
            });
        }

        public bool CreateClanRole(Guid clanId, string name, int level)
        {
            // clan does not exist
            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return false;

            // role already exists
            var existingRoles = gameData.GetClanRoles(clan.Id);
            if (existingRoles.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return false;

            gameData.Add(new DataModels.ClanRole
            {
                Id = Guid.NewGuid(),
                Name = name,
                Level = level,
                ClanId = clanId
            });

            return true;
        }

        public IReadOnlyList<Player> GetClanMembers(Guid clanId)
        {
            // clan does not exist
            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return null;

            // empty clan
            var memberships = gameData.GetClanMemberships(clanId);
            if (memberships == null || memberships.Count == 0)
                return new List<Player>();

            return memberships
                .Select(x => gameData.GetCharacter(x.CharacterId))
                .Select(x => ModelMapper.Map(gameData.GetUser(x.UserId), gameData, x))
                .ToList();
        }

        public IReadOnlyList<Player> GetInvitedPlayers(Guid clanId)
        {
            // clan does not exist
            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return null;

            // no invites
            var invites = gameData.GetClanInvites(clanId);
            if (invites == null || invites.Count == 0)
                return new List<Player>();

            return invites
                .Select(x => gameData.GetCharacter(x.CharacterId))
                .Select(x => ModelMapper.Map(gameData.GetUser(x.UserId), gameData, x))
                .ToList();
        }

        public IReadOnlyList<ClanRole> GetClanRoles(Guid clanId)
        {
            // clan does not exist
            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return null;

            return gameData.GetClanRoles(clanId)
                .OrderByDescending(x => x.Level)
                .ThenBy(x => x.Name)
                .Select(x => ModelMapper.Map(x))
                .ToList();
        }

        public void UpdateMemberRole(Guid clanId, Guid characterId, Guid roleId)
        {
            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return;

            // character does not exist
            var character = gameData.GetCharacter(characterId);
            if (character == null)
                return;

            // character already a member of a clan
            var membership = gameData.GetClanMembership(characterId);
            if (membership == null)
                return;

            if (membership.ClanId != clanId)
                return;

            var role = gameData.GetClanRole(roleId);
            if (role == null)
                return;

            membership.ClanRoleId = role.Id;
        }

        public bool RemoveClanMember(Guid clanId, Guid characterId)
        {
            // clan does not exist
            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return false;

            // character does not exist
            var character = gameData.GetCharacter(characterId);
            if (character == null)
                return false;

            // character already a member of a clan
            var membership = gameData.GetClanMembership(characterId);
            if (membership == null)
                return false;

            if (membership.ClanId != clanId)
                return false;

            var appearance = gameData.GetAppearance(character.SyntyAppearanceId);
            appearance.Cape = -1;

            gameData.Remove(membership);
            return true;
        }

        public bool RemoveClanRole(Guid roleId)
        {
            // role does not exist
            var role = gameData.GetClanRole(roleId);
            if (role == null)
                return false;

            // member has this role assigned.
            var memberships = gameData.GetClanMemberships(role.ClanId);
            foreach (var member in memberships)
            {
                if (member.ClanRoleId == roleId)
                    return false;
            }

            gameData.Remove(role);
            return true;
        }

        public bool UpdateClanRole(Guid roleId, string newName, int newLevel)
        {
            // role does not exist
            var role = gameData.GetClanRole(roleId);
            if (role == null)
                return false;

            role.Name = newName;
            role.Level = newLevel;
            return true;
        }

        public bool UpdateClanLogo(Guid clanId, string logoImageFile)
        {
            // clan does not exist
            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return false;

            clan.Logo = logoImageFile;
            return true;
        }

        public bool UpdateClanName(Guid clanId, string newName)
        {
            // clan does not exist
            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return false;

            clan.Name = newName;
            return true;
        }
    }
}
