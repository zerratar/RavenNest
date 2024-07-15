using RavenNest.Models;
using System;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Game
{
    public interface IClanManager
    {
        bool AcceptClanInvite(Guid inviteId);
        bool AcceptClanInvite(Guid inviteId, out DataModels.Clan clan, out DataModels.ClanRole role);
        JoinClanResult AcceptClanInvite(Guid characterId, string argument);
        bool AddClanMember(Guid clanId, Guid characterId, Guid roleId);
        bool AddClanRole(Guid characterId, string name, int level);
        bool AssignMemberClanRole(Guid clanId, Guid characterId, Guid roleId);
        bool CanChangeClanName(Guid clanId);
        Clan CreateClan(Guid ownerUserId, string name, string logoImageFile);
        Clan CreateClan(string userId, string name, string logo);
        bool CreateClanRole(Guid clanId, string name, int level);
        ClanDeclineResult DeclineClanInvite(Guid characterId, string argument);
        ChangeRoleResult DemoteClanMember(Guid senderCharacterId, Guid characterId, string argument);
        DataModels.Clan FindClan(string query, string platform);
        Clan GetClan(Guid clanId);
        Clan GetClanByCharacter(Guid characterId);
        Clan GetClanByOwnerUserId(Guid userId);
        ClanInfo GetClanInfo(Guid characterId);
        IReadOnlyList<Player> GetClanMembers(Guid clanId);
        DataModels.ClanRole GetClanRoleByCharacterId(Guid characterId);
        TypedClanRolePermissions GetClanRolePermissions(ClanRole role);
        TypedClanRolePermissions GetClanRolePermissions(DataModels.ClanRole role);
        TypedClanRolePermissions GetClanRolePermissionsByCharacterId(Guid characterId);
        IReadOnlyList<ClanRole> GetClanRoles(Guid clanId);
        ClanStats GetClanStats(Guid characterId);
        IReadOnlyList<Player> GetInvitedPlayers(Guid clanId);
        int GetNameChangeCount(Guid clanId);
        TypedClanRolePermissions GetOwnerPermissions();
        bool IsClanOwner(Guid characterId);
        JoinClanResult JoinClan(Guid clanOwnerUserId, Guid characterId);
        JoinClanResult JoinClan(string clanOwnerId, string platform, Guid characterId);
        ClanLeaveResult LeaveClan(Guid characterId);
        ChangeRoleResult PromoteClanMember(Guid senderCharacterId, Guid characterId, string argument);
        bool RemoveClanInvite(Guid inviteId);
        bool RemoveClanMember(Guid clanId, Guid characterId);
        bool RemoveClanRole(Guid roleId);
        bool RemoveClanRole(Guid characterId, Guid roleId);
        bool RemovePlayerInvite(Guid inviteId);
        bool RemovePlayerInvite(Guid clanId, Guid characterId);
        bool RenameClanName(Guid characterId, string newName);
        bool RenameClanRole(Guid characterId, Guid roleId, string newName);
        bool ResetNameChangeCounter(Guid clanId);
        ClanInviteResult SendPlayerInvite(Guid senderCharacterId, Guid characterId);
        bool SendPlayerInvite(Guid clanId, Guid characterId, Guid? senderUserId = null);
        bool UpdateClanLogo(Guid clanId, string logoImageFile);
        bool UpdateClanName(Guid clanId, string newName);
        bool UpdateClanRole(Guid roleId, string newName, int newLevel);
        void UpdateMemberRole(Guid clanId, Guid characterId, Guid roleId);
    }
}