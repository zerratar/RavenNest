using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Game.ClanBank;
using RavenNest.BusinessLogic.Game.Trading;
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
        private readonly ClanPageManager pages;
        private readonly RavenNest.BusinessLogic.Game.ClanBank.ClanBankManager bank;
        private readonly PlayerInventoryProvider inventoryProvider;
        public const int MaxClanNameLength = 40;

        public ClanService(
            GameData gameData,
            IClanManager clanManager,
            RavenNest.BusinessLogic.Game.ClanBank.ClanBankManager bank,
            PlayerInventoryProvider inventoryProvider,
            ClanPageManager pages,
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.clanManager = clanManager;
            this.bank = bank;
            this.inventoryProvider = inventoryProvider;
            this.pages = pages;
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

        #region Join policy and join requests
        // docs/clan-pages-design.md, step 2: the clan page's Join / Request to join, and the Requests tab on /clan.

        /// <summary>What the signed in visitor can do about joining this clan, per character; null when signed out.</summary>
        public ClanJoinOptions GetJoinOptions(Guid clanId)
        {
            var session = GetSession();
            if (!session.Authenticated)
                return null;

            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return null;

            var options = new ClanJoinOptions { Policy = (ClanJoinPolicy)clan.JoinPolicy };
            foreach (var character in gameData.GetCharactersByUserId(session.UserId).OrderBy(x => x.CharacterIndex))
            {
                var membership = gameData.GetClanMembership(character.Id);
                var inClan = membership != null ? gameData.GetClan(membership.ClanId) : null;
                options.Characters.Add(new ClanJoinCharacter
                {
                    CharacterId = character.Id,
                    Name = CharacterName(character),
                    InClan = inClan?.Name,
                    InThisClan = inClan != null && inClan.Id == clanId,
                    WaitingRequestId = gameData.GetClanJoinRequestsByCharacter(character.Id).FirstOrDefault(x => x.ClanId == clanId && x.State == 0)?.Id,
                    Invited = gameData.GetClanInvitesByCharacter(character.Id).Any(x => x.ClanId == clanId),
                });
            }

            return options;
        }

        /// <summary>
        ///     Joins an open clan (or one the character is invited to), or files a request with the clan that takes
        ///     them. The character must be the signed in user's.
        /// </summary>
        public ClanJoinOutcome Join(Guid clanId, Guid characterId, string message)
        {
            var session = GetSession();
            if (!session.Authenticated)
                return ClanJoinOutcome.Fail("Sign in first.");

            var clan = gameData.GetClan(clanId);
            var character = gameData.GetCharacter(characterId);
            if (clan == null || character == null || character.UserId != session.UserId)
                return ClanJoinOutcome.Fail("That character or clan could not be found.");

            if ((ClanJoinPolicy)clan.JoinPolicy == ClanJoinPolicy.OnRequest)
            {
                var request = clanManager.RequestToJoin(clanId, characterId, message, out var error);
                return request != null
                    ? new ClanJoinOutcome { Success = true, RequestSent = true, Message = "Request sent. " + clan.Name + "'s officers will answer it, and you will get a notification." }
                    : ClanJoinOutcome.Fail(error);
            }

            var result = clanManager.JoinClan(clan.UserId, characterId);
            if (result.Success)
                return new ClanJoinOutcome { Success = true, Message = CharacterName(character) + " joined " + clan.Name + " as " + (result.Role?.Name ?? "a member") + "." };

            return ClanJoinOutcome.Fail(result.Failure switch
            {
                JoinClanFailure.AlreadyInClan => CharacterName(character) + " is already in a clan. Leave it first.",
                JoinClanFailure.InviteOnly => clan.Name + " only takes members by invite.",
                _ => "Could not join " + clan.Name + " right now.",
            });
        }

        public bool WithdrawJoinRequest(Guid requestId)
        {
            var session = GetSession();
            return session.Authenticated && clanManager.WithdrawJoinRequest(requestId, session.UserId);
        }

        /// <summary>The requests waiting on the clan, for ranks that may invite; null for anyone else.</summary>
        public IReadOnlyList<ClanJoinRequestEntry> GetJoinRequests(Guid clanId, Guid? actingCharacterId)
        {
            var mine = GetMyPermissions(clanId, actingCharacterId);
            if (mine == null || !mine.CanCreateInvite)
                return null;

            var output = new List<ClanJoinRequestEntry>();
            foreach (var request in clanManager.GetWaitingJoinRequests(clanId))
            {
                var character = gameData.GetCharacter(request.CharacterId);
                if (character == null)
                    continue;

                var skills = gameData.GetCharacterSkills(character.SkillsId);
                output.Add(new ClanJoinRequestEntry
                {
                    Id = request.Id,
                    CharacterId = character.Id,
                    Name = CharacterName(character),
                    AvatarUrl = "/api/twitch/logo/" + (gameData.GetUserAccess(character.UserId, "twitch")?.PlatformId ?? character.UserId.ToString()),
                    CombatLevel = skills != null ? GameData.GetCombatLevel(skills) : 0,
                    Message = request.Message,
                    Created = request.Created,
                });
            }

            return output;
        }

        public ClanJoinOutcome AnswerJoinRequest(Guid clanId, Guid requestId, bool accept, Guid? actingCharacterId)
        {
            var session = GetSession();
            var mine = GetMyPermissions(clanId, actingCharacterId);
            if (mine == null || !mine.CanCreateInvite)
                return ClanJoinOutcome.Fail("Your rank may not answer requests.");

            var request = gameData.GetClanJoinRequest(requestId);
            if (request == null || request.ClanId != clanId)
                return ClanJoinOutcome.Fail("That request is not for this clan.");

            var name = CharacterName(gameData.GetCharacter(request.CharacterId));
            if (!accept)
                return clanManager.DeclineJoinRequest(requestId, session.UserId)
                    ? new ClanJoinOutcome { Success = true, Message = "Turned down " + name + "." }
                    : ClanJoinOutcome.Fail("That request has already been answered.");

            return clanManager.AcceptJoinRequest(requestId, session.UserId, out var error)
                ? new ClanJoinOutcome { Success = true, Message = name + " is in the clan now." }
                : ClanJoinOutcome.Fail(error);
        }

        public ClanJoinPolicy GetJoinPolicy(Guid clanId) => clanManager.GetJoinPolicy(clanId);

        /// <summary>Who may join; for ranks with "Can change who may join" (the CanMakePublic permission).</summary>
        public bool SetJoinPolicy(Guid clanId, ClanJoinPolicy policy, Guid? actingCharacterId)
        {
            var mine = GetMyPermissions(clanId, actingCharacterId);
            return mine != null && mine.CanMakePublic && clanManager.SetJoinPolicy(clanId, policy);
        }

        private static string CharacterName(DataModels.Character character)
        {
            if (character == null) return "Someone";
            var name = character.Name;
            if (!string.IsNullOrEmpty(character.Identifier) && !character.Identifier.All(char.IsDigit))
                name += " (" + character.Identifier + ")";
            if (character.CharacterIndex > 0)
                name += " #" + character.CharacterIndex;
            return name;
        }
        #endregion

        #region The clan's page: privacy, about, looks, Top 8, news, reports
        // docs/clan-pages-design.md, steps 3 and 4. The page's settings are the owner's, as the Look tab is; news is
        // for the owner and ranks with "Post clan news".

        private bool OwnsClan(Guid clanId)
        {
            var session = GetSession();
            return session.Authenticated && clanManager.GetClanByOwnerUserId(session.UserId)?.Id == clanId;
        }

        /// <summary>The page's settings for the owner's editor; null for anyone else.</summary>
        public ClanPageEditorState GetPageEditor(Guid clanId)
        {
            if (!OwnsClan(clanId))
                return null;

            var settings = pages.Get(clanId);
            var clan = gameData.GetClan(clanId);
            var top = pages.GetTopMembers(clanId);
            return new ClanPageEditorState
            {
                HideMembers = settings?.HideMembers == true,
                HidePlaying = settings?.HidePlaying == true,
                HideJoined = settings?.HideJoined == true,
                HideRanks = settings?.HideRanks == true,
                HideProgress = settings?.HideProgress == true,
                About = settings?.About ?? "",
                AboutHidden = settings?.AboutHidden == true,
                AccentColor = settings?.AccentColor ?? "",
                BannerUrl = clan?.BannerImageId != null ? "/api/clan/image/" + clan.BannerImageId : null,
                TopMembers = top.ToList(),
                Members = gameData.GetClanMemberships(clanId)
                    .Select(x => gameData.GetCharacter(x.CharacterId))
                    .Where(x => x != null)
                    .Select(x => new ClanPageMemberOption { CharacterId = x.Id, Name = CharacterName(x) })
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            };
        }

        public bool SavePagePrivacy(Guid clanId, bool hideMembers, bool hidePlaying, bool hideJoined, bool hideRanks, bool hideProgress)
        {
            if (!OwnsClan(clanId)) return false;
            pages.SetPrivacy(clanId, hideMembers, hidePlaying, hideJoined, hideRanks, hideProgress);
            return true;
        }

        public bool SavePageAbout(Guid clanId, string about)
        {
            if (!OwnsClan(clanId)) return false;
            pages.SetAbout(clanId, about);
            return true;
        }

        public bool SavePageAccent(Guid clanId, string color) => OwnsClan(clanId) && pages.SetAccentColor(clanId, color);

        public bool SavePageTopMembers(Guid clanId, IEnumerable<Guid> characterIds)
        {
            if (!OwnsClan(clanId)) return false;
            pages.SetTopMembers(clanId, characterIds);
            return true;
        }

        /// <summary>Every post of the clan for those who may write them (hidden ones marked); null for anyone else.</summary>
        public IReadOnlyList<ClanNewsPost> GetNewsForEditor(Guid clanId, Guid? actingCharacterId)
        {
            var mine = GetMyPermissions(clanId, actingCharacterId);
            if (mine == null || !mine.CanPostNews)
                return null;

            return pages.GetPosts(clanId, includeMembersOnly: true, includeHidden: true)
                .Select(x => new ClanNewsPost
                {
                    Id = x.Id,
                    Title = x.Title,
                    Body = x.Body,
                    MembersOnly = x.MembersOnly,
                    Pinned = x.Pinned,
                    Hidden = x.Hidden,
                    Created = x.Created,
                    Edited = x.Edited,
                    Author = x.AuthorCharacterId != Guid.Empty ? CharacterName(gameData.GetCharacter(x.AuthorCharacterId)) : gameData.GetUser(x.AuthorUserId)?.UserName,
                })
                .ToList();
        }

        /// <summary>Writes a new post (empty id) or changes one of the clan's; the reason when it cannot.</summary>
        public string SaveNewsPost(Guid clanId, Guid? actingCharacterId, Guid postId, string title, string body, bool membersOnly, bool pinned)
        {
            var session = GetSession();
            var mine = GetMyPermissions(clanId, actingCharacterId);
            if (mine == null || !mine.CanPostNews)
                return "Your rank may not post news.";

            string error;
            if (postId == Guid.Empty)
                pages.CreatePost(clanId, session.UserId, OwnsClan(clanId) ? Guid.Empty : actingCharacterId ?? Guid.Empty, title, body, membersOnly, pinned, out error);
            else
                pages.UpdatePost(clanId, postId, title, body, membersOnly, pinned, out error);
            return error;
        }

        public bool DeleteNewsPost(Guid clanId, Guid? actingCharacterId, Guid postId)
        {
            var mine = GetMyPermissions(clanId, actingCharacterId);
            return mine != null && mine.CanPostNews && pages.DeletePost(clanId, postId);
        }

        /// <summary>Anyone signed in reports a post, or the about text (empty post id), for an admin to look at.</summary>
        public string ReportClanContent(Guid clanId, Guid postId, string reason)
        {
            var session = GetSession();
            if (!session.Authenticated)
                return "Sign in to report something.";

            return pages.Report(clanId, postId, session.UserId, reason, out var error)
                ? null
                : error;
        }

        /// <summary>Open reports for the admin page; null for anyone but an admin.</summary>
        public IReadOnlyList<ClanReportEntry> GetOpenClanReports()
        {
            var session = GetSession();
            if (!session.Authenticated || !session.Administrator)
                return null;

            var open = pages.GetOpenReports();
            return open
                .GroupBy(x => (x.ClanId, x.PostId))
                .Select(group =>
                {
                    var first = group.First();
                    var clan = gameData.GetClan(first.ClanId);
                    var post = first.PostId != Guid.Empty ? gameData.GetClanPost(first.PostId) : null;
                    return new ClanReportEntry
                    {
                        ReportId = first.Id,
                        ClanName = clan?.Name ?? "(gone)",
                        ClanUrl = clan != null ? "/clans/" + Uri.EscapeDataString(clan.Name) : null,
                        What = post != null ? "News post" : "About text",
                        Title = post?.Title,
                        Text = post != null ? post.Body : pages.Get(first.ClanId)?.About,
                        Reports = group.Count(),
                        Reasons = group.Select(x => (gameData.GetUser(x.ReporterUserId)?.UserName ?? "someone") + (string.IsNullOrWhiteSpace(x.Reason) ? "" : ": " + x.Reason)).ToList(),
                        Created = group.Min(x => x.Created),
                    };
                })
                .OrderBy(x => x.Created)
                .ToList();
        }

        public bool ResolveClanReport(Guid reportId, bool hide)
        {
            var session = GetSession();
            return session.Authenticated && session.Administrator && pages.Resolve(reportId, session.UserId, hide);
        }
        #endregion

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

        #region Clan bank

        /// <summary>The character's name, for saying who is acting rather than showing an id.</summary>
        public string GetCharacterName(Guid characterId) =>
            characterId == Guid.Empty ? null : gameData.GetCharacter(characterId)?.Name;

        /// <summary>
        ///     What the clan holds, with the item resolved so the page can draw it.
        /// </summary>
        public IReadOnlyList<ClanBankEntry> GetBankItems(Guid clanId)
        {
            var rows = bank.GetItems(clanId);
            var result = new List<ClanBankEntry>();

            foreach (var row in rows)
            {
                var item = gameData.GetItem(row.ItemId);
                if (item == null) continue;

                result.Add(new ClanBankEntry { Row = row, Item = item });
            }

            return result.OrderBy(x => x.Name).ToList();
        }

        /// <summary>
        ///     What a character is carrying that could go into the bank.
        /// </summary>
        /// <remarks>
        ///     Soulbound items are left out rather than shown and refused. The bank will not take
        ///     them, and offering something that cannot be done is a worse answer than not offering
        ///     it.
        /// </remarks>
        public IReadOnlyList<ClanBankEntry> GetDepositable(Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null) return Array.Empty<ClanBankEntry>();

            var result = new List<ClanBankEntry>();

            foreach (var stack in inventoryProvider.Get(characterId).GetUnequippedItems())
            {
                if (stack.Soulbound) continue;

                var item = gameData.GetItem(stack.ItemId);
                if (item == null) continue;

                result.Add(new ClanBankEntry { Inventory = stack, Item = item });
            }

            return result.OrderBy(x => x.Name).ToList();
        }

        public IReadOnlyList<ClanBankLogEntry> GetBankLog(Guid clanId)
        {
            var result = new List<ClanBankLogEntry>();

            foreach (var row in bank.GetLog(clanId))
            {
                var character = gameData.GetCharacter(row.CharacterId);
                var item = gameData.GetItem(row.ItemId);

                result.Add(new ClanBankLogEntry
                {
                    Time = row.Time,
                    Amount = row.Amount,
                    Who = character?.Name ?? "someone who has left",
                    ItemName = item?.Name ?? "an item that no longer exists"
                });
            }

            return result;
        }

        /// <summary>How much more this character may take out today. -1 is no limit.</summary>
        public long RemainingToday(Guid characterId, Guid clanId) =>
            characterId == Guid.Empty ? 0 : bank.RemainingToday(characterId, clanId);

        public TradeResult Deposit(Guid characterId, Guid clanId, StackKey stack, long amount) =>
            bank.Deposit(characterId, clanId, stack, amount);

        public TradeResult Withdraw(Guid characterId, Guid clanId, StackKey stack, long amount) =>
            bank.Withdraw(characterId, clanId, stack, amount);

        /// <summary>The per rank daily allowances, for the owner's editor.</summary>
        public IReadOnlyList<ClanBankAllowance> GetAllowances(Guid clanId)
        {
            var result = new List<ClanBankAllowance>();
            var seen = new HashSet<int>();

            foreach (var role in gameData.GetClanRoles(clanId).OrderByDescending(x => x.Level))
            {
                if (!seen.Add(role.Level)) continue;

                var limit = gameData.GetClanBankLimit(clanId, role.Level);

                result.Add(new ClanBankAllowance
                {
                    RoleName = role.Name,
                    RoleLevel = role.Level,
                    ItemsPerDay = limit?.ItemsPerDay ?? ClanBankDefaults.ForRoleLevel(role.Level)
                });
            }

            return result;
        }

        /// <summary>
        ///     Sets a rank's daily allowance. Owner only, since it decides how much of the clan's
        ///     property everybody else can carry off.
        /// </summary>
        public bool SetAllowance(Guid clanId, int roleLevel, int itemsPerDay)
        {
            var session = GetSession();
            if (!session.Authenticated) return false;

            var clan = clanManager.GetClan(clanId);
            if (clan == null || clan.OwnerUserId != session.UserId) return false;

            if (itemsPerDay < ClanBankDefaults.Unlimited) itemsPerDay = ClanBankDefaults.Unlimited;

            var limit = gameData.GetClanBankLimit(clanId, roleLevel);
            if (limit == null)
            {
                var created = ClanBankDefaults.For(clanId, roleLevel);
                created.ItemsPerDay = itemsPerDay;
                gameData.Add(created);
                return true;
            }

            limit.ItemsPerDay = itemsPerDay;
            return true;
        }

        #endregion
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

    /// <summary>
    ///     One line of a bank list, from either side: a clan holding or something a character is
    ///     carrying. One shape, because the two tables show the same columns.
    /// </summary>
    public class ClanBankEntry
    {
        public DataModels.ClanBankItem Row { get; set; }
        public ReadOnlyInventoryItem Inventory { get; set; }
        public DataModels.Item Item { get; set; }

        /// <summary>For the item helpers, which take the model rather than the entity.</summary>
        public Guid ItemId => Item?.Id ?? Guid.Empty;

        public bool FromBank => Row != null;

        public long Amount => Row?.Amount ?? Inventory.Amount;

        public string Name
        {
            get
            {
                var given = Row != null ? Row.Name : Inventory.Name;
                return string.IsNullOrWhiteSpace(given) ? Item?.Name ?? "unknown item" : given;
            }
        }

        public string Enchantment => Row != null ? Row.Enchantment : Inventory.Enchantment;

        public bool IsEnchanted => !string.IsNullOrEmpty(Enchantment);

        public StackKey Stack => Row != null ? Row.Key() : Inventory.Key();
    }

    public class ClanBankLogEntry
    {
        public DateTime Time { get; set; }

        /// <summary>Positive is a deposit, negative a withdrawal.</summary>
        public long Amount { get; set; }

        public string Who { get; set; }
        public string ItemName { get; set; }

        public bool IsWithdrawal => Amount < 0;
    }

    public class ClanBankAllowance
    {
        public string RoleName { get; set; }
        public int RoleLevel { get; set; }
        public int ItemsPerDay { get; set; }
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

    public class ClanPageEditorState
    {
        public bool HideMembers { get; set; }
        public bool HidePlaying { get; set; }
        public bool HideJoined { get; set; }
        public bool HideRanks { get; set; }
        public bool HideProgress { get; set; }
        public string About { get; set; }
        public bool AboutHidden { get; set; }
        public string AccentColor { get; set; }
        public string BannerUrl { get; set; }
        public List<Guid> TopMembers { get; set; }
        public List<ClanPageMemberOption> Members { get; set; }
    }

    public class ClanPageMemberOption
    {
        public Guid CharacterId { get; set; }
        public string Name { get; set; }
    }

    public class ClanNewsPost
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public bool MembersOnly { get; set; }
        public bool Pinned { get; set; }
        public bool Hidden { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Edited { get; set; }
        public string Author { get; set; }
    }

    public class ClanReportEntry
    {
        public Guid ReportId { get; set; }
        public string ClanName { get; set; }
        public string ClanUrl { get; set; }
        public string What { get; set; }
        public string Title { get; set; }
        public string Text { get; set; }
        public int Reports { get; set; }
        public List<string> Reasons { get; set; }
        public DateTime Created { get; set; }
    }

    public class ClanJoinOptions
    {
        public ClanJoinPolicy Policy { get; set; }
        public List<ClanJoinCharacter> Characters { get; } = new List<ClanJoinCharacter>();
    }

    public class ClanJoinCharacter
    {
        public Guid CharacterId { get; set; }
        public string Name { get; set; }

        /// <summary>The clan the character is in, or null.</summary>
        public string InClan { get; set; }
        public bool InThisClan { get; set; }
        public Guid? WaitingRequestId { get; set; }
        public bool Invited { get; set; }
        public bool CanAct => InClan == null && WaitingRequestId == null;
    }

    public class ClanJoinOutcome
    {
        public bool Success { get; set; }
        public bool RequestSent { get; set; }
        public string Message { get; set; }
        public static ClanJoinOutcome Fail(string message) => new ClanJoinOutcome { Message = message };
    }

    public class ClanJoinRequestEntry
    {
        public Guid Id { get; set; }
        public Guid CharacterId { get; set; }
        public string Name { get; set; }
        public string AvatarUrl { get; set; }
        public int CombatLevel { get; set; }
        public string Message { get; set; }
        public DateTime Created { get; set; }
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
