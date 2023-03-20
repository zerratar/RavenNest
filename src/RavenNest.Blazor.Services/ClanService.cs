using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class ClanService : RavenNestService
    {
        private readonly GameData gameData;
        private readonly ClanManager clanManager;

        public ClanService(
            GameData gameData,
            ClanManager clanManager,
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.clanManager = clanManager;
        }

        public Clan GetClan()
        {
            var session = GetSession();
            if (!session.Authenticated)
                return null;

            return this.clanManager.GetClanByOwnerUserId(session.UserId);
        }

        public IReadOnlyList<ClanMember> RemoveMember(Guid clanId, Guid characterId)
        {
            var session = GetSession();
            if (!session.Authenticated)
                return null;

            var user = gameData.GetUser(session.UserId);
            if (user == null) return null;

            var character = gameData.GetCharacter(characterId);
            if (character == null) return null;
            if (character.UserId != user.Id) return null;

            this.clanManager.RemoveClanMember(clanId, characterId);
            return GetMembers(clanId);
        }

        public IReadOnlyList<ClanMember> RemoveInvite(Guid clanId, Guid characterId)
        {
            var session = GetSession();
            if (!session.Authenticated)
                return null;

            this.clanManager.RemovePlayerInvite(clanId, characterId);
            return GetMembers(clanId);
        }

        public async Task<IReadOnlyList<ClanMember>> UpdateMemberRoleAsync(Guid clanId, Guid characterId, Guid roleId)
        {
            var session = GetSession();
            if (!session.Authenticated)
                return null;

            return await Task.Run(() =>
            {
                this.clanManager.UpdateMemberRole(clanId, characterId, roleId);
                return GetMembers(clanId);
            });
        }

        public IReadOnlyList<ClanMember> InvitePlayer(Guid clanId, Guid characterId)
        {
            var session = GetSession();
            if (!session.Authenticated)
                return null;
            var user = gameData.GetUser(session.UserId);
            this.clanManager.SendPlayerInvite(clanId, characterId, user.Id);
            return GetMembers(clanId);
        }

        public IReadOnlyList<ClanRole> GetRoles(Guid clanId)
        {
            var session = GetSession();
            if (!session.Authenticated)
                return null;

            return clanManager.GetClanRoles(clanId);
        }

        public IReadOnlyList<ClanMember> GetMembers(Guid clanId)
        {
            var output = new List<ClanMember>();
            var session = GetSession();
            if (!session.Authenticated)
                return null;

            var clan = clanManager.GetClan(clanId);
            if (clan == null)
                return output;

            var members = this.clanManager.GetClanMembers(clanId);
            var invites = this.clanManager.GetInvitedPlayers(clanId);

            foreach (var member in members)
                output.Add(new ClanMember { Player = member });

            foreach (var member in invites)
                output.Add(new ClanMember { Player = member, InvitationPending = true });

            return output;
        }

        public async Task<IReadOnlyList<ClanInvite>> AcceptClanInviteAsync(Guid inviteId)
        {
            return await Task.Run(async () =>
            {
                var session = GetSession();
                if (!session.Authenticated)
                    return null;

                var user = gameData.GetUser(session.UserId);
                if (user == null)
                    return null;

                clanManager.AcceptClanInvite(inviteId);
                return await GetClanInvitesAsync();
            });
        }
        public async Task<IReadOnlyList<ClanInvite>> DeclineClanInviteAsync(Guid inviteId)
        {
            return await Task.Run(async () =>
            {
                var session = GetSession();
                if (!session.Authenticated)
                    return null;

                var user = gameData.GetUser(session.UserId);
                if (user == null)
                    return null;

                clanManager.RemovePlayerInvite(inviteId);
                return await GetClanInvitesAsync();
            });
        }

        public async Task<IReadOnlyList<ClanInvite>> GetClanInvitesAsync()
        {
            return await Task.Run(() =>
            {
                var session = GetSession();
                if (!session.Authenticated)
                    return null;
                var user = gameData.GetUser(session.UserId);
                if (user == null)
                    return null;

                var allInvites = new List<ClanInvite>();
                var characters = gameData.GetCharacters(x => x.UserId == user.Id);
                foreach (var c in characters)
                {
                    var invites = gameData.GetClanInvitesByCharacter(c.Id);
                    foreach (var invite in invites)
                    {
                        var clan = gameData.GetClan(invite.ClanId);
                        var owner = gameData.GetUser(clan.UserId);

                        var userId = invite.InviterUserId.GetValueOrDefault();
                        var inviter = gameData.GetUser(userId);
                        if (inviter == null)
                        {
                            // could be a removed user or character id by accident.
                            var cha = gameData.GetCharacter(userId);
                            if (cha != null)
                            {
                                inviter = gameData.GetUser(cha.UserId);
                            }
                        }

                        allInvites.Add(new ClanInvite
                        {
                            Character = ModelMapper.MapForWebsite(c, gameData, user),
                            InviteId = invite.Id,
                            Inviter = inviter,
                            Created = invite.Created,
                            ClanName = clan.Name,
                            ClanLogo = clan.Logo ?? $"/api/clan/logo/{owner.Id}"
                        });
                    }
                }

                return allInvites;
            });
        }

        public bool ResetNameChangeCounter(Guid clanId)
        {
            return this.clanManager.ResetNameChangeCounter(clanId);
        }

        public int GetNameChangeCount(Guid clanId)
        {
            return this.clanManager.GetNameChangeCount(clanId);
        }

        public bool CanChangeClanName(Guid clanId)
        {
            return this.clanManager.CanChangeClanName(clanId);
        }

        public bool UpdateClanName(Guid clanId, string newName)
        {
            var session = GetSession();
            if (!session.Authenticated)
                return false;

            var clan = clanManager.GetClan(clanId);
            if (clan == null)
                return false;

            if (clan.OwnerUserId != session.UserId)
                return false;

            return this.clanManager.UpdateClanName(clanId, newName);
        }
        public Clan CreateClan(CreateClanModel model)
        {
            var session = GetSession();
            if (!session.Authenticated)
                return null;

            return this.clanManager.CreateClan(session.TwitchUserId, model.Name, model.Logo);
        }
    }

    public class ClanInvite
    {
        public Guid InviteId { get; set; }
        public string ClanLogo { get; set; }
        public string ClanName { get; set; }
        public Player Character { get; set; }
        public DataModels.User Inviter { get; set; }
        public DateTime Created { get; set; }
    }

    public class ClanMember
    {
        public Player Player { get; set; }
        public bool InvitationPending { get; set; }
    }

    public class CreateClanModel
    {
        [Required]
        [NameValidator(AllowedCharacters = "qwertyuiopasdfghjklzxcvbnm-_ 1234567890[]()%")]
        public string Name { get; set; }
        public string Logo { get; set; }
    }

    public class NameValidator : ValidationAttribute
    {
        public string AllowedCharacters { get; set; }

        protected override ValidationResult IsValid(
            object value,
            ValidationContext validationContext)
        {
            var v = value?.ToString();
            if (string.IsNullOrEmpty(v))
                return new ValidationResult("Value cannot be empty.");

            if (string.IsNullOrEmpty(AllowedCharacters))
                return null;

            var allowed = AllowedCharacters.ToCharArray();
            var used = v.ToLower().ToCharArray();
            foreach (var u in used)
            {
                if (!allowed.Contains(u))
                    return new ValidationResult("Name can only contain " + AllowedCharacters);
            }

            return null;
        }
    }
}
