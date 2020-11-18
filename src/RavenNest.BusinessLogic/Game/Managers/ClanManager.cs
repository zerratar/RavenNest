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

        public ClanManager(IGameData gameData)
        {
            this.gameData = gameData;
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
                UserId = ownerUserId
            };
            gameData.Add(clan);
            return ModelMapper.Map(gameData, clan);
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

        public IReadOnlyList<ClanRole> GetClanRoles(Guid clanId)
        {
            // clan does not exist
            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return null;

            return gameData.GetClanRoles(clanId)
                .Select(ModelMapper.Map)
                .ToList();
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
