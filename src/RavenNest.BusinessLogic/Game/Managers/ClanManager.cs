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

        public int GetNameChangeCount(Guid clanId)
        {
            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return 0;

            return clan.NameChangeCount;
        }

        public bool CanChangeClanName(Guid clanId)
        {
            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return false;

            return clan.CanChangeName || clan.NameChangeCount < 2;
        }

        public bool ResetNameChangeCounter(Guid clanId)
        {
            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return false;

            clan.CanChangeName = true;
            clan.NameChangeCount = 0;
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
            {
                // check if the clan still exists.
                var joinedClan = gameData.GetClan(membership.ClanId);
                if (joinedClan != null && gameData.GetUser(joinedClan.UserId) != null)
                    return false;
            }

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

        public Clan GetClanByUserId(Guid userId)
        {
            var clan = gameData.GetClanByUser(userId);
            if (clan == null) return null;
            return ModelMapper.Map(gameData, clan);
        }

        public Clan GetClanByUserId(string userId)
        {
            var user = gameData.GetUserByTwitchId(userId);
            if (user == null) return null;
            var clan = gameData.GetClanByUser(user.Id);
            if (clan == null) return null;
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
            var user = gameData.GetUserByTwitchId(userId);
            if (user == null)
                return null;

            return CreateClan(user.Id, name, logo);
        }

        public Clan CreateClan(Guid ownerUserId, string name, string logoImageFile)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            name = name.Trim();

            // already have a clan
            var clan = gameData.GetClanByUser(ownerUserId);
            if (clan != null)
                return null;

            // no such user
            var user = gameData.GetUser(ownerUserId);
            if (user == null)
                return null;

            var clans = gameData.GetClans();
            if (clans.FirstOrDefault(x => x.Name?.Trim().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase) ?? false) != null)
                return null;

            clan = new DataModels.Clan()
            {
                Id = Guid.NewGuid(),
                CanChangeName = true,
                Logo = logoImageFile,
                Level = 1,
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

        public bool AddClanRole(Guid characterId, string name, int level)
        {
            var permissions = GetClanRolePermissionsByCharacterId(characterId);
            if (permissions == null || !permissions.CanAddClanRole)
                return false;

            var clan = GetClanByCharacter(characterId);

            return CreateClanRole(clan.Id, name, level);
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

        public bool RemoveClanRole(Guid characterId, Guid roleId)
        {
            var permissions = GetClanRolePermissionsByCharacterId(characterId);
            if (permissions == null || !permissions.CanRemoveClanRole)
                return false;

            return RemoveClanRole(roleId);
        }

        public bool RenameClanName(Guid characterId, string newName)
        {
            var permissions = GetClanRolePermissionsByCharacterId(characterId);
            if (permissions == null || !permissions.CanRenameClan)
                return false;

            return UpdateClanName(GetClanByCharacter(characterId).Id, newName);
        }

        public bool RenameClanRole(Guid characterId, Guid roleId, string newName)
        {
            var permissions = GetClanRolePermissionsByCharacterId(characterId);
            if (permissions == null || !permissions.CanRenameClanRole)
                return false;

            var role = gameData.GetClanRole(roleId);
            if (role == null)
                return false;

            role.Name = newName;
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
            if (string.IsNullOrWhiteSpace(newName))
                return false;

            newName = newName.Trim();
            if (newName.Length > 40)
                return false;

            // clan does not exist
            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return false;

            if (clan.Name.Trim().Equals(newName, StringComparison.Ordinal))
            {
                clan.Name = newName;
                return true;
            }

            if (clan.CanChangeName || clan.NameChangeCount < 2)
            {
                clan.Name = newName;
                clan.CanChangeName = false;
                clan.NameChangeCount++;
                return true;
            }

            return false;
        }

        public ClanStats GetClanStats(Guid characterId)
        {
            var clan = GetClanByCharacter(characterId);
            if (clan == null) return null;

            var skills = gameData.GetClanSkills(clan.Id);
            var skillInfos = new List<ClanSkillInfo>();
            foreach (var s in skills)
            {
                skillInfos.Add(new ClanSkillInfo
                {
                    Level = s.Level,
                    Name = gameData.GetSkill(s.SkillId).Name
                });
            }

            var owner = gameData.GetUserByTwitchId(clan.Owner);
            var result = new ClanStats
            {
                ClanSkills = skillInfos,
                Level = clan.Level,
                Name = clan.Name,
                OwnerName = owner.UserName,
                ProgressToLevel = (float)(clan.Experience / GameMath.ExperienceForLevel(clan.Level + 1))
            };

            return result;
        }

        public ClanInfo GetClanInfo(Guid characterId)
        {
            var clan = GetClanByCharacter(characterId);
            if (clan == null) return null;

            var memberships = gameData.GetClanMemberships(clan.Id);
            var roles = gameData.GetClanRoles(clan.Id);
            var roleInfos = new List<ClanRoleInfo>();
            foreach (var role in roles)
            {
                var roleInfo = new ClanRoleInfo();
                roleInfo.Name = role.Name;
                roleInfo.Level = role.Level;

                foreach (var m in memberships)
                {
                    if (m.ClanRoleId == role.Id)
                    {
                        roleInfo.MemberCount++;
                    }
                }

                roleInfos.Add(roleInfo);
            }

            var owner = gameData.GetUserByTwitchId(clan.Owner);
            var result = new ClanInfo
            {
                Name = clan.Name,
                OwnerName = owner.UserName,
                Roles = roleInfos
            };
            return result;
        }

        public bool SendPlayerInvite(Guid characterId, Guid senderCharacterId)
        {
            var clan = GetClanByCharacter(characterId);
            if (clan == null) return false;

            var permissions = GetClanRolePermissionsByCharacterId(characterId);
            if (permissions == null || !permissions.CanCreateInvite)
                return false;

            return SendPlayerInvite(clan.Id, characterId, senderCharacterId);
        }

        public bool AcceptClanInvite(Guid characterId, string argument)
        {
            var invites = gameData.GetClanInvitesByCharacter(characterId);
            var invite = invites.OrderByDescending(x => x.Created).FirstOrDefault();
            if (invite == null)
                return false;

            return AcceptClanInvite(invite.Id);
        }

        public bool DeclineClanInvite(Guid characterId, string argument)
        {
            var invites = gameData.GetClanInvitesByCharacter(characterId);
            var invite = invites.OrderByDescending(x => x.Created).FirstOrDefault();
            if (invite == null)
                return false;

            return RemovePlayerInvite(invite.Id);
        }

        public bool PromoteClanMember(Guid senderCharacterId, Guid characterId, string argument)
        {
            var senderClan = GetClanByCharacter(senderCharacterId);
            if (senderClan == null) return false;

            int currentRoleLevel = 0;
            int senderRoleLevel = 9999;
            var canAssignAllRoles = true;
            if (!IsClanOwner(senderCharacterId))
            {
                // check if we have permission to demote the player.
                var role = GetClanRoleByCharacterId(characterId);
                if (role == null) return false;

                var permissions = GetClanRolePermissions(role);
                if (!permissions.CanAssignRoles)
                    return false;

                var targetCharacterClan = GetClanByCharacter(characterId);
                if (targetCharacterClan.Id != senderClan.Id) return false;

                var currentRole = GetClanRoleByCharacterId(characterId);
                if (currentRole == null) return false;

                if (currentRole.Level <= 1) return false; // player cannot be demoted anymore, that would make them inactive.

                canAssignAllRoles = permissions.CanAssignAllRoles;
                currentRoleLevel = currentRole.Level;
                senderRoleLevel = role.Level;
            }

            // find which role can be assigned
            var roles = GetClanRoles(senderClan.Id);
            var targetRole = roles.Where(x => x.Level > currentRoleLevel).OrderBy(x => System.Math.Abs(currentRoleLevel - x.Level)).FirstOrDefault();
            if (targetRole == null) return false;

            if ((targetRole.Level >= senderRoleLevel && !canAssignAllRoles) || currentRoleLevel >= senderRoleLevel)
            {
                return false; // user does not have enough permission to assign the player to a higher role.
            }

            if (GetClanRolePermissions(targetRole).CanSeeClanDetails)
            {
                return AssignMemberClanRole(senderClan.Id, characterId, targetRole.Id);
            }

            return false;
        }

        public bool DemoteClanMember(Guid senderCharacterId, Guid characterId, string argument)
        {
            var senderClan = GetClanByCharacter(senderCharacterId);
            if (senderClan == null) return false;

            int currentRoleLevel = 0;
            int senderRoleLevel = 9999;
            if (!IsClanOwner(senderCharacterId))
            {
                // check if we have permission to demote the player.
                // check if we have permission to demote the player.
                var role = GetClanRoleByCharacterId(characterId);
                if (role == null) return false;

                var permissions = GetClanRolePermissions(role);
                if (!permissions.CanAssignRoles)
                    return false;

                var targetCharacterClan = GetClanByCharacter(characterId);
                if (targetCharacterClan.Id != senderClan.Id) return false;

                var currentRole = GetClanRoleByCharacterId(characterId);
                if (currentRole == null) return false;
                if (currentRole.Level <= 1) return false; // player cannot be demoted anymore, that would make them inactive.

                currentRoleLevel = currentRole.Level;
                senderRoleLevel = role.Level;
            }

            // find which role can be assigned
            var roles = GetClanRoles(senderClan.Id);
            var targetRole = roles.Where(x => x.Level < currentRoleLevel).OrderBy(x => System.Math.Abs(currentRoleLevel - x.Level)).FirstOrDefault();
            if (targetRole == null) return false;

            if (currentRoleLevel >= senderRoleLevel)
            {
                return false;
            }

            if (GetClanRolePermissions(targetRole).CanSeeClanDetails)
            {
                return AssignMemberClanRole(senderClan.Id, characterId, targetRole.Id);
            }

            return false;
        }

        public bool JoinClan(string clanOwnerId, Guid characterId)
        {
            var clan = FindClan(clanOwnerId);
            if (clan == null) return false;
            var charClan = GetClanByCharacter(characterId);
            if (charClan != null) return false;

            if (clan.IsPublic)
                return false;

            var roles = GetClanRoles(clan.Id);
            if (roles == null) return false;

            var targetRole = roles.OrderBy(x => x.Level).FirstOrDefault(x => x.Level > 0);
            if (targetRole == null) return false;

            return AddClanMember(clan.Id, characterId, targetRole.Id);
        }

        public bool LeaveClan(Guid characterId)
        {
            var clan = GetClanByCharacter(characterId);
            if (clan == null) return false;
            return RemoveClanMember(clan.Id, characterId);
        }

        public DataModels.ClanRole GetClanRoleByCharacterId(Guid characterId)
        {
            var membership = gameData.GetClanMembership(characterId);
            if (membership == null) return null;
            return gameData.GetClanRole(membership.ClanRoleId);
        }

        public DataModels.Clan FindClan(string query)
        {
            foreach (var clan in gameData.GetClans())
            {
                if (Match(clan, query))
                {
                    return clan;
                }
            }
            return null;
        }

        public TypedClanRolePermissions GetClanRolePermissionsByCharacterId(Guid characterId)
        {
            if (IsClanOwner(characterId))
            {
                return GetOwnerPermissions();
            }

            var role = GetClanRoleByCharacterId(characterId);
            if (role == null) return null;
            return GetClanRolePermissions(role);
        }

        public bool IsClanOwner(Guid characterId)
        {
            var role = GetClanRoleByCharacterId(characterId);
            if (role == null) return false;
            var c = gameData.GetCharacter(characterId);
            var targetClan = gameData.GetClanByUser(c.UserId);
            return targetClan != null && targetClan.Id == role.ClanId;
        }

        public TypedClanRolePermissions GetOwnerPermissions()
        {
            return ClanRolePermissionsBuilder.Parse("11111111111111111111111111");
        }

        public TypedClanRolePermissions GetClanRolePermissions(ClanRole role)
        {
            var permissions = gameData.GetClanRolePermissions(role.Id);
            if (permissions == null)
            {
                permissions = GenerateDefaultPermissions(role);
            }

            return ResolvePermissions(permissions);
        }

        public TypedClanRolePermissions GetClanRolePermissions(DataModels.ClanRole role)
        {
            var permissions = gameData.GetClanRolePermissions(role.Id);
            if (permissions == null)
            {
                permissions = GenerateDefaultPermissions(role);
            }

            return ResolvePermissions(permissions);
        }

        private TypedClanRolePermissions ResolvePermissions(DataModels.ClanRolePermissions permissions)
        {
            return ClanRolePermissionsBuilder.Parse(permissions.Permissions);
        }

        private DataModels.ClanRolePermissions GenerateDefaultPermissions(DataModels.ClanRole role)
        {
            var permissions = new DataModels.ClanRolePermissions
            {
                Id = Guid.NewGuid(),
                ClanRoleId = role.Id,
                Permissions = GenerateDefaultPermissions(role.Level)
            };

            gameData.Add(permissions);

            return permissions;
        }

        private DataModels.ClanRolePermissions GenerateDefaultPermissions(ClanRole role)
        {
            var permissions = new DataModels.ClanRolePermissions
            {
                Id = Guid.NewGuid(),
                ClanRoleId = role.Id,
                Permissions = GenerateDefaultPermissions(role.Level)
            };

            gameData.Add(permissions);

            return permissions;
        }

        private string GenerateDefaultPermissions(int level)
        {
            // for this, we use a binary form but with a string representation
            // permission types: (note: owner can always do everything so no permission required.)
            // 0 rename clan
            // 1 add roles
            // 2 remove roles
            // 3 rename roles
            // 4 assign roles sudo // this can change to any role
            // 5 assign roles      // this can change to only roles lower level of themselves
            // - kick members
            // - kick all members
            // 6 change public join
            // 7 create invite
            // 8 delete invite
            // 9 use clan skills
            // 10 see clan details

            var builder = new ClanRolePermissionsBuilder();

            // inactive.
            if (level == 0)
            {
                return builder.GenerateString();
            }

            if (level > 0)
            {
                builder.Values.CanSeeClanDetails = true;
                builder.Values.CanUseClanSkills = true;
            }

            if (level > 1)
            {
                //
                builder.Values.CanCreateInvite = true;
            }

            if (level > 2)
            {
                builder.Values.CanAssignRoles = true;
                //builder.Values.CanAssignAllRoles = true;
                builder.Values.CanDeleteInvite = true;
                builder.Values.CanKickMembers = true;
            }

            //if (level > 3)
            //{

            //}

            return builder.GenerateString();
        }

        private bool Match(DataModels.Clan clan, string query)
        {
            var owner = gameData.GetUser(clan.UserId);
            if (owner == null) return false;
            return owner.UserId.Equals(query, StringComparison.OrdinalIgnoreCase) ||
                owner.UserName.Equals(query, StringComparison.OrdinalIgnoreCase) ||
                clan.Name.Equals(query, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class TypedClanRolePermissions
    {
        public bool CanSeeClanDetails { get; set; }
        public bool CanUseClanSkills { get; set; }
        public bool CanAssignRoles { get; set; }
        public bool CanAssignAllRoles { get; set; }
        public bool CanKickMembers { get; set; }
        public bool CanKickAllMembers { get; set; }
        public bool CanRenameClan { get; set; }
        public bool CanAddClanRole { get; set; }
        public bool CanRemoveClanRole { get; set; }
        public bool CanRenameClanRole { get; set; }
        public bool CanCreateInvite { get; set; }
        public bool CanDeleteInvite { get; set; }
        public bool CanMakePublic { get; set; }
    }

    public class ClanRolePermissionsBuilder
    {
        public readonly TypedClanRolePermissions Values = new TypedClanRolePermissions();

        public static TypedClanRolePermissions Parse(string permissions)
        {
            var values = new TypedClanRolePermissions();
            var index = 0;
            values.CanRenameClan = Bool(permissions[index++]);
            values.CanAddClanRole = Bool(permissions[index++]);
            values.CanRemoveClanRole = Bool(permissions[index++]);
            values.CanRenameClanRole = Bool(permissions[index++]);
            values.CanAssignAllRoles = Bool(permissions[index++]);
            values.CanAssignRoles = Bool(permissions[index++]);
            values.CanKickMembers = Bool(permissions[index++]);
            values.CanKickAllMembers = Bool(permissions[index++]);
            values.CanMakePublic = Bool(permissions[index++]);
            values.CanCreateInvite = Bool(permissions[index++]);
            values.CanDeleteInvite = Bool(permissions[index++]);
            values.CanUseClanSkills = Bool(permissions[index++]);
            values.CanSeeClanDetails = Bool(permissions[index++]);
            return values;
        }

        internal string GenerateString()
        {
            // 0 rename clan
            // 1 add roles
            // 2 remove roles
            // 3 rename roles
            // 4 assign roles sudo // this can change to any role
            // 5 assign roles      // this can change to only roles lower level of themselves
            // - kick members
            // - kick all members
            // 6 change public join
            // 7 create invite
            // 8 delete invite
            // 9 use clan skills
            // 10 see clan details

            return
                Bin(Values.CanRenameClan) +
                Bin(Values.CanAddClanRole) +
                Bin(Values.CanRemoveClanRole) +
                Bin(Values.CanRenameClanRole) +
                Bin(Values.CanAssignAllRoles) +
                Bin(Values.CanAssignRoles) +
                Bin(Values.CanKickMembers) +
                Bin(Values.CanKickAllMembers) +
                Bin(Values.CanMakePublic) +
                Bin(Values.CanCreateInvite) +
                Bin(Values.CanDeleteInvite) +
                Bin(Values.CanUseClanSkills) +
                Bin(Values.CanSeeClanDetails);
        }
        private static bool Bool(char b) => b == '1';
        private string Bin(bool b)
        {
            return b ? "1" : "0";
        }
    }
}
