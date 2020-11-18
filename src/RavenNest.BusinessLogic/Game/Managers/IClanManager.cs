using RavenNest.Models;
using System;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Game
{
    public interface IClanManager
    {
        IReadOnlyList<Player> GetClanMembers(Guid clanId);
        IReadOnlyList<ClanRole> GetClanRoles(Guid clanId);
        Clan CreateClan(Guid ownerUserId, string name, string logoImageFile);
        bool CreateClanRole(Guid clanId, string name, int level);
        bool RemoveClanRole(Guid roleId);
        bool UpdateClanRole(Guid roleId, string newName, int newLevel);
        bool UpdateClanName(Guid clanId, string newName);
        bool UpdateClanLogo(Guid clanId, string logoImageFile);
        bool AssignMemberClanRole(Guid clanId, Guid characterId, Guid roleId);
        bool AddClanMember(Guid clanId, Guid characterId, Guid roleId);
        bool RemoveClanMember(Guid clanId, Guid characterId);
    }
}
