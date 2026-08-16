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
        private readonly IClanManager clanManager;
        public const int MaxClanNameLength = 40;

        public ClanService(
            GameData gameData,
            IClanManager clanManager,
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

        /// <summary>
        ///     Every clan this user has a foot in, owned clan first.
        /// </summary>
        /// <remarks>
        ///     One entry per character in a clan, not one per clan, because a clan rank belongs to a
        ///     character rather than to the person behind it. Two characters in the same clan at
        ///     different ranks are two different sets of permissions, and which one applies is a
        ///     question with an answer only if somebody says which character is acting.
        ///
        ///     <para>
        ///     This replaces GetMyClan, which returned the owned clan or else the first clan found
        ///     while walking the characters in whatever order they came back. A user with characters
        ///     in two clans got whichever happened to be first, which is how somebody ended up
        ///     managing a clan that was not the one they were looking at.
        ///     </para>
        ///
        ///     <para>
        ///     A clan you own but have no character in still appears, with no acting character. You
        ///     are its owner either way, and losing the ability to manage your own clan by not
        ///     having joined it would be a strange thing to discover.
        ///     </para>
        /// </remarks>
        public IReadOnlyList<ClanMembership> GetMyClanMemberships()
        {
            var session = GetSession();
            if (!session.Authenticated)
                return Array.Empty<ClanMembership>();

            var owned = this.clanManager.GetClanByOwnerUserId(session.UserId);
            var found = new List<ClanMembership>();

            foreach (var character in gameData.GetCharactersByUserId(session.UserId))
            {
                var clan = this.clanManager.GetClanByCharacter(character.Id);
                if (clan == null) continue;

                var membership = gameData.GetClanMembership(character.Id);
                var role = membership == null ? null : gameData.GetClanRole(membership.ClanRoleId);

                found.Add(new ClanMembership
                {
                    Clan = clan,
                    CharacterId = character.Id,
                    CharacterName = character.Name,
                    CharacterIndex = character.CharacterIndex,
                    IsOwner = owned != null && owned.Id == clan.Id,
                    RoleName = role?.Name
                });
            }

            // Your own clan is always first, and within a clan the characters keep their usual order.
            var ordered = found
                .OrderByDescending(x => x.IsOwner)
                .ThenBy(x => x.CharacterIndex)
                .ToList();

            if (owned != null && !ordered.Any(x => x.Clan.Id == owned.Id))
            {
                ordered.Insert(0, new ClanMembership
                {
                    Clan = owned,
                    IsOwner = true,
                    RoleName = "Owner"
                });
            }

            return ordered;
        }

        /// <summary>
        ///     The rank level the acting character holds in a clan, or null when it is not in it.
        ///     The owner sits above every rank.
        /// </summary>
        /// <remarks>
        ///     This used to match on the user rather than the character, taking the first character
        ///     of theirs it found in the roster. With two characters in one clan at different ranks
        ///     that picked one arbitrarily, and the arbitrary choice decided who they could kick.
        /// </remarks>
        private int? MyRoleLevel(Guid clanId, Guid? actingCharacterId, IReadOnlyList<ClanMember> members)
        {
            var session = GetSession();
            if (!session.Authenticated)
                return null;

            var owned = clanManager.GetClanByOwnerUserId(session.UserId);
            if (owned != null && owned.Id == clanId)
                return int.MaxValue;

            if (actingCharacterId == null)
                return null;

            var mine = members?.FirstOrDefault(x => !x.InvitationPending && x.Player.Id == actingCharacterId.Value);
            return mine?.Player.ClanRole?.Level;
        }

        private static int RoleLevelOf(IReadOnlyList<ClanMember> members, Guid characterId) =>
            members?.FirstOrDefault(x => x.Player.Id == characterId)?.Player.ClanRole?.Level ?? 0;

        /// <summary>
        ///     Removing a member used to require that the character was your own, so a clan owner
        ///     could never remove anybody from the website: the call returned null, which the
        ///     roster then rendered as a permanent loading spinner.
        ///
        ///     Leaving is still always allowed, because it is your own character. Removing anyone
        ///     else needs the permission, and the plain kick permission only reaches ranks below
        ///     your own so an Officer cannot remove the Leader.
        /// </summary>
        public IReadOnlyList<ClanMember> RemoveMember(Guid clanId, Guid characterId, Guid? actingCharacterId)
        {
            var session = GetSession();
            if (!session.Authenticated)
                return null;

            var user = gameData.GetUser(session.UserId);
            if (user == null) return null;

            var character = gameData.GetCharacter(characterId);
            if (character == null) return null;

            var members = GetMembers(clanId);
            var leaving = character.UserId == user.Id;

            if (!leaving)
            {
                var mine = GetMyPermissions(clanId, actingCharacterId);
                var myLevel = MyRoleLevel(clanId, actingCharacterId, members) ?? -1;
                var targetLevel = RoleLevelOf(members, characterId);

                var allowed = mine != null &&
                    (mine.CanKickAllMembers || (mine.CanKickMembers && targetLevel < myLevel));

                // Refusing returns the roster unchanged rather than null, so a refusal reads as
                // "nothing happened" instead of blanking the page.
                if (!allowed)
                    return members;
            }

            this.clanManager.RemoveClanMember(clanId, characterId);
            return GetMembers(clanId);
        }

        public IReadOnlyList<ClanMember> RemoveInvite(Guid clanId, Guid characterId, Guid? actingCharacterId)
        {
            var session = GetSession();
            if (!session.Authenticated)
                return null;

            var members = GetMembers(clanId);
            var mine = GetMyPermissions(clanId, actingCharacterId);
            if (mine == null || !mine.CanDeleteInvite)
                return members;

            this.clanManager.RemovePlayerInvite(clanId, characterId);
            return GetMembers(clanId);
        }

        /// <summary>
        ///     Changing someone's rank had no authorisation check at all, so any signed in user
        ///     could restructure any clan they knew the id of. The plain permission only reaches
        ///     ranks below your own, in both directions: you cannot demote someone at or above
        ///     you, and you cannot promote anyone into a rank at or above your own.
        /// </summary>
        public async Task<IReadOnlyList<ClanMember>> UpdateMemberRoleAsync(Guid clanId, Guid characterId, Guid roleId, Guid? actingCharacterId)
        {
            var session = GetSession();
            if (!session.Authenticated)
                return null;

            return await Task.Run(() =>
            {
                var members = GetMembers(clanId);
                var mine = GetMyPermissions(clanId, actingCharacterId);
                if (mine == null)
                    return members;

                if (!mine.CanAssignAllRoles)
                {
                    var myLevel = MyRoleLevel(clanId, actingCharacterId, members) ?? -1;
                    var targetLevel = RoleLevelOf(members, characterId);
                    var newRole = gameData.GetClanRole(roleId);

                    if (!mine.CanAssignRoles || newRole == null || newRole.ClanId != clanId)
                        return members;

                    if (targetLevel >= myLevel || newRole.Level >= myLevel)
                        return members;
                }

                this.clanManager.UpdateMemberRole(clanId, characterId, roleId);
                return GetMembers(clanId);
            });
        }

        public IReadOnlyList<ClanMember> InvitePlayer(Guid clanId, Guid characterId, Guid? actingCharacterId)
        {
            var session = GetSession();
            if (!session.Authenticated)
                return null;

            var members = GetMembers(clanId);
            var mine = GetMyPermissions(clanId, actingCharacterId);
            if (mine == null || !mine.CanCreateInvite)
                return members;

            var user = gameData.GetUser(session.UserId);
            if (user == null) return members;

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

        /// <summary>
        ///     What a rank is allowed to do.
        ///
        ///     Clan role permissions have existed in <see cref="ClanManager"/> since long before
        ///     this, with thirteen typed permissions, defaults backfilled on startup and the chat
        ///     commands already enforcing them. The website never read any of it: every check on
        ///     the site was "are you the founder", so a clan of forty was administered by exactly
        ///     one person and the ranks below were decoration.
        /// </summary>
        public TypedClanRolePermissions GetRolePermissions(Guid roleId)
        {
            var session = GetSession();
            if (!session.Authenticated)
                return null;

            var role = gameData.GetClanRole(roleId);
            return role == null ? null : clanManager.GetClanRolePermissions(role);
        }

        /// <summary>
        ///     The permissions the signed in user holds in this clan, resolved from whichever of
        ///     their characters is a member. The founder gets everything.
        /// </summary>
        /// <summary>
        ///     What the acting character may do in this clan.
        /// </summary>
        /// <remarks>
        ///     Ownership is checked first now. It used to be the fallback after walking the
        ///     characters, so an owner who also had a low ranked character in their own clan was
        ///     handed that character's permissions and locked out of their own clan.
        ///
        ///     <para>
        ///     The character has to belong to the signed in user and has to be in this clan. Both are
        ///     checked here rather than trusted from the page, because the character id arrives from
        ///     the browser and a tab is not an authorisation.
        ///     </para>
        /// </remarks>
        public TypedClanRolePermissions GetMyPermissions(Guid clanId, Guid? actingCharacterId)
        {
            var session = GetSession();
            if (!session.Authenticated)
                return null;

            var owned = clanManager.GetClanByOwnerUserId(session.UserId);
            if (owned != null && owned.Id == clanId)
                return clanManager.GetOwnerPermissions();

            if (actingCharacterId == null)
                return null;

            var character = gameData.GetCharacter(actingCharacterId.Value);
            if (character == null || character.UserId != session.UserId)
                return null;

            var clan = clanManager.GetClanByCharacter(character.Id);
            if (clan == null || clan.Id != clanId)
                return null;

            return clanManager.GetClanRolePermissionsByCharacterId(character.Id);
        }

        /// <summary>
        ///     Rewrites a rank's permissions. Only someone who may edit ranks can do it, and the
        ///     clan's own owner is never editable, since a clan that can lock its founder out is a
        ///     support ticket waiting to happen.
        /// </summary>
        public bool UpdateRolePermissions(Guid clanId, Guid roleId, TypedClanRolePermissions values, Guid? actingCharacterId)
        {
            var session = GetSession();
            if (!session.Authenticated || values == null)
                return false;

            var mine = GetMyPermissions(clanId, actingCharacterId);
            if (mine == null || !mine.CanRenameClanRole)
                return false;

            var role = gameData.GetClanRole(roleId);
            if (role == null || role.ClanId != clanId)
                return false;

            var stored = gameData.GetClanRolePermissions(roleId);
            if (stored == null)
                return false;

            stored.Permissions = ClanRolePermissionsBuilder.Generate(values);
            return true;
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

            // Which streams are live right now, keyed by the streamer. Built once rather than
            // asked per member: GetSessionByCharacterId scans every character in the game, which
            // is fine for one lookup and not fine for two hundred.
            var liveStreams = new Dictionary<Guid, string>();
            foreach (var gameSession in gameData.GetActiveSessions())
            {
                var owner = gameData.GetUser(gameSession.UserId);
                if (owner != null)
                    liveStreams[gameSession.UserId] = owner.UserName;
            }

            foreach (var member in members)
                output.Add(new ClanMember { Player = member, PlayingOn = GetLiveStreamFor(member, liveStreams) });

            foreach (var member in invites)
                output.Add(new ClanMember { Player = member, InvitationPending = true });

            return output;
        }

        /// <summary>
        ///     The stream a character is currently playing on, or null when they are not playing.
        ///
        ///     A character is locked to the session it joined, so the lock plus the set of live
        ///     sessions is enough. The clan pages had no way of telling a member who plays daily
        ///     from one who left a year ago, which is most of what a clan wants to know.
        /// </summary>
        private string GetLiveStreamFor(Player member, Dictionary<Guid, string> liveStreams)
        {
            if (liveStreams.Count == 0)
                return null;

            var character = gameData.GetCharacter(member.Id);
            if (character?.UserIdLock == null)
                return null;

            return liveStreams.TryGetValue(character.UserIdLock.Value, out var streamer) ? streamer : null;
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

        public int GetMaxClanNameLength() => MaxClanNameLength;

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

            if (newName.Length > MaxClanNameLength)
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

    /// <summary>
    ///     One character's place in one clan, or a clan you own with nobody of yours in it.
    /// </summary>
    public class ClanMembership
    {
        public Clan Clan { get; set; }

        /// <summary>
        ///     The character acting in this clan. Null only for a clan you own but have not joined
        ///     with any character.
        /// </summary>
        public Guid? CharacterId { get; set; }

        public string CharacterName { get; set; }

        public int CharacterIndex { get; set; }

        /// <summary>Whether the signed in user founded this clan.</summary>
        public bool IsOwner { get; set; }

        /// <summary>The rank that character holds, for showing on the tab.</summary>
        public string RoleName { get; set; }

        /// <summary>What to call this tab when the same clan appears more than once.</summary>
        public string ActingAs => CharacterName ?? "you";
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

        /// <summary>
        ///     The stream this character is playing on right now, or null when they are not
        ///     playing. Null is also what an invited player gets, since they have not joined.
        /// </summary>
        public string PlayingOn { get; set; }

        public bool IsPlaying => !string.IsNullOrEmpty(PlayingOn);
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
