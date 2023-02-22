using RavenNest.Models;
using System;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Game
{
    public interface IClanManager
    {
        IReadOnlyList<Player> GetClanMembers(Guid clanId);
        IReadOnlyList<Player> GetInvitedPlayers(Guid clanId);
        IReadOnlyList<ClanRole> GetClanRoles(Guid clanId);
        Clan GetClan(Guid clanId);
        Clan GetClanByCharacter(Guid characterId);
        Clan GetClanByUserId(string userId);
        Clan GetClanByUserId(Guid userId);
        Clan CreateClan(string userId, string name, string logo);
        Clan CreateClan(Guid ownerUserId, string name, string logoImageFile);
        bool AcceptClanInvite(Guid inviteId);
        bool CreateClanRole(Guid clanId, string name, int level);
        bool RemoveClanRole(Guid roleId);
        bool UpdateClanRole(Guid roleId, string newName, int newLevel);
        bool UpdateClanName(Guid clanId, string newName);
        bool UpdateClanLogo(Guid clanId, string logoImageFile);
        bool AssignMemberClanRole(Guid clanId, Guid characterId, Guid roleId);
        bool AddClanMember(Guid clanId, Guid characterId, Guid roleId);
        bool RemoveClanMember(Guid clanId, Guid characterId);
        bool SendPlayerInvite(Guid clanId, Guid characterId, Guid? sender = null);
        bool RemovePlayerInvite(Guid clanId, Guid characterId);
        bool RemovePlayerInvite(Guid inviteId);
        void UpdateMemberRole(Guid clanId, Guid characterId, Guid roleId);
        bool CanChangeClanName(Guid clanId);
        int GetNameChangeCount(Guid clanId);
        bool ResetNameChangeCounter(Guid clanId);


        ClanStats GetClanStats(Guid characterId);
        ClanInfo GetClanInfo(Guid characterId);
        bool SendPlayerInvite(Guid senderCharacterId, Guid characterId);
        JoinClanResult AcceptClanInvite(Guid characterId, string argument);
        bool DeclineClanInvite(Guid characterId, string argument);
        ChangeRoleResult PromoteClanMember(Guid senderCharacterId, Guid characterId, string argument);
        ChangeRoleResult DemoteClanMember(Guid senderCharacterId, Guid characterId, string argument);
        JoinClanResult JoinClan(string clanOwnerId, Guid characterId);
        bool LeaveClan(Guid characterId);
    }
}
