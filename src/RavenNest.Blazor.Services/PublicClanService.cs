using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.Sessions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Blazor.Services
{
    /// <summary>
    ///     The public clan list (/clans) and clan pages (/clans/{name}), readable without signing in
    ///     (the game's docs/clan-pages-design.md, step 1). Small view models only: the dashboard's
    ///     Player objects carry whole inventories and are not for a page anyone can open.
    /// </summary>
    public class PublicClanService : RavenNestService
    {
        private readonly GameData gameData;
        private readonly RavenNest.BusinessLogic.Game.ClanPageManager pages;

        // the list is the same for everyone and costs a pass over every membership, so it is kept a minute
        private static readonly TimeSpan ListCacheDuration = TimeSpan.FromSeconds(60);
        private readonly object listLock = new object();
        private IReadOnlyList<PublicClanSummary> cachedList;
        private DateTime cachedListAt;

        public PublicClanService(
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider,
            GameData gameData,
            RavenNest.BusinessLogic.Game.ClanPageManager pages)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.pages = pages;
        }

        /// <summary>Every clan with members, as the list shows them.</summary>
        public IReadOnlyList<PublicClanSummary> GetClans()
        {
            lock (listLock)
            {
                if (cachedList != null && DateTime.UtcNow - cachedListAt < ListCacheDuration)
                    return cachedList;
            }

            var live = GetLiveStreams();
            var list = new List<PublicClanSummary>();
            foreach (var clan in gameData.GetClans())
            {
                var memberships = gameData.GetClanMemberships(clan.Id);
                if (memberships == null || memberships.Count == 0)
                    continue;

                var summary = Summarize(clan, memberships, live);
                if (summary != null)
                    list.Add(summary);
            }

            lock (listLock)
            {
                cachedList = list;
                cachedListAt = DateTime.UtcNow;
            }

            return list;
        }

        /// <summary>Drops the cached list, after a change the list shows (someone joined a clan, a policy changed).</summary>
        public void Forget()
        {
            lock (listLock)
                cachedList = null;
        }

        /// <summary>One clan's page, found by its name (any case) or its id; null when there is no such clan.</summary>
        public PublicClanPage GetClan(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            key = key.Trim();
            var clan = Guid.TryParse(key, out var id)
                ? gameData.GetClan(id)
                : gameData.GetClans().FirstOrDefault(x => string.Equals(x.Name?.Trim(), key, StringComparison.OrdinalIgnoreCase));
            if (clan == null)
                return null;

            var live = GetLiveStreams();
            var memberships = gameData.GetClanMemberships(clan.Id) ?? Array.Empty<DataModels.CharacterClanMembership>();
            var summary = Summarize(clan, memberships, live);
            if (summary == null)
                return null;

            var session = GetSession();
            var visitorIsMember = session.Authenticated && session.UserId != Guid.Empty
                && (clan.UserId == session.UserId || memberships.Any(x => gameData.GetCharacter(x.CharacterId)?.UserId == session.UserId));

            // what the public may see (step 3); the clan's own members see everything
            var settings = pages.Get(clan.Id);
            bool Shows(bool hidden) => visitorIsMember || !hidden;
            var showMembers = Shows(settings?.HideMembers == true);
            var showPlaying = Shows(settings?.HidePlaying == true);
            var showJoined = Shows(settings?.HideJoined == true);
            var showRanks = Shows(settings?.HideRanks == true);
            var showProgress = Shows(settings?.HideProgress == true);
            if (!showPlaying)
                summary.PlayingHidden = true;

            var roles = (gameData.GetClanRoles(clan.Id) ?? Array.Empty<DataModels.ClanRole>()).ToDictionary(x => x.Id);
            var members = new List<PublicClanMember>();
            foreach (var membership in memberships)
            {
                var character = gameData.GetCharacter(membership.CharacterId);
                if (character == null)
                    continue;

                roles.TryGetValue(membership.ClanRoleId, out var role);
                members.Add(new PublicClanMember
                {
                    CharacterId = character.Id,
                    Name = NameOf(character),
                    AvatarUrl = "/api/twitch/logo/" + (gameData.GetUserAccess(character.UserId, "twitch")?.PlatformId ?? character.UserId.ToString()),
                    Rank = role?.Name ?? "Member",
                    RankLevel = role?.Level ?? 0,
                    Joined = showJoined ? membership.Joined : DateTime.MinValue,
                    PlayingOn = showPlaying ? PlayingOn(character, live) : null,
                    IsOwner = character.UserId == clan.UserId,
                });
            }

            // the owner first, then by rank, then the longest serving
            members = members
                .OrderByDescending(x => x.IsOwner)
                .ThenByDescending(x => x.RankLevel)
                .ThenBy(x => x.Joined)
                .ToList();

            var ranks = roles.Values
                .OrderByDescending(x => x.Level)
                .Select(x => new PublicClanRank
                {
                    Name = x.Name,
                    Level = x.Level,
                    Members = members.Where(m => m.Rank == x.Name && m.RankLevel == x.Level).ToList(),
                })
                .ToList();

            var skills = new List<PublicClanSkill>();
            foreach (var s in gameData.GetClanSkills(clan.Id) ?? Array.Empty<DataModels.ClanSkill>())
            {
                var skill = gameData.GetSkill(s.SkillId);
                if (skill == null) continue;
                skills.Add(new PublicClanSkill { Name = skill.Name, Level = s.Level, Experience = s.Experience, MaxLevel = skill.MaxLevel });
            }

            var byId = members.ToDictionary(x => x.CharacterId);
            var top = pages.GetTopMembers(clan.Id).Where(byId.ContainsKey).Select(x => byId[x]).ToList();

            var posts = pages.GetPosts(clan.Id, includeMembersOnly: visitorIsMember, includeHidden: visitorIsMember)
                .Select(x => MapPost(x, clan))
                .ToList();

            var aboutHidden = settings?.AboutHidden == true;
            var owner = gameData.GetUser(clan.UserId);
            return new PublicClanPage
            {
                Summary = summary,
                Experience = clan.Experience,
                Skills = skills,
                Ranks = showRanks ? ranks : new List<PublicClanRank>(),
                Members = showMembers ? members : new List<PublicClanMember>(),
                OwnerLiveOn = owner != null && live.ContainsKey(owner.Id) ? live[owner.Id] : null,
                VisitorIsMember = visitorIsMember,
                VisitorSignedIn = session.Authenticated,
                MembersHidden = !showMembers,
                RanksHidden = !showRanks,
                ProgressHidden = !showProgress,
                ShowsRank = showRanks,
                ShowsJoined = showJoined,
                ShowsPlaying = showPlaying,
                About = aboutHidden && !visitorIsMember ? null : settings?.About,
                AboutHidden = aboutHidden,
                AccentColor = settings?.AccentColor,
                BannerUrl = clan.BannerImageId != null ? "/api/clan/image/" + clan.BannerImageId : null,
                TopMembers = top,
                Posts = posts,
                VisitorCount = settings?.VisitorCount ?? 0,
            };
        }

        /// <summary>Counts this visit (once a day per visitor) and returns the clan's visitor count.</summary>
        public long CountVisit(Guid clanId)
        {
            var session = GetSession();
            string key = null;
            if (session.Authenticated && session.UserId != Guid.Empty)
            {
                key = "u:" + session.UserId;
            }
            else
            {
                // a hash, kept in memory for the day only: enough to count a visitor once without keeping addresses
                var address = Context?.Connection?.RemoteIpAddress?.ToString();
                if (!string.IsNullOrEmpty(address))
                    key = "a:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(address + clanId)));
            }

            return pages.CountVisit(clanId, key);
        }

        private PublicClanPost MapPost(DataModels.ClanPost post, DataModels.Clan clan)
        {
            var character = post.AuthorCharacterId != Guid.Empty ? gameData.GetCharacter(post.AuthorCharacterId) : null;
            var user = gameData.GetUser(post.AuthorUserId);
            return new PublicClanPost
            {
                Id = post.Id,
                Title = post.Title,
                Body = post.Body,
                Author = character != null ? NameOf(character) : (string.IsNullOrEmpty(user?.DisplayName) ? user?.UserName : user.DisplayName) ?? "The clan",
                AuthorIsOwner = post.AuthorUserId == clan.UserId,
                Created = post.Created,
                Edited = post.Edited,
                MembersOnly = post.MembersOnly,
                Pinned = post.Pinned,
                Hidden = post.Hidden,
            };
        }

        private PublicClanSummary Summarize(DataModels.Clan clan, IReadOnlyList<DataModels.CharacterClanMembership> memberships, Dictionary<Guid, string> live)
        {
            var owner = gameData.GetUser(clan.UserId);
            if (owner == null)
                return null;

            var playing = 0;
            foreach (var membership in memberships)
            {
                var character = gameData.GetCharacter(membership.CharacterId);
                if (character != null && PlayingOn(character, live) != null)
                    playing++;
            }

            var enchanting = 0;
            var enchantingMax = 0;
            var hidePlaying = pages.Get(clan.Id)?.HidePlaying == true;
            foreach (var s in gameData.GetClanSkills(clan.Id) ?? Array.Empty<DataModels.ClanSkill>())
            {
                var skill = gameData.GetSkill(s.SkillId);
                if (skill?.Name == "Enchanting")
                {
                    enchanting = s.Level;
                    enchantingMax = skill.MaxLevel;
                }
            }

            return new PublicClanSummary
            {
                Id = clan.Id,
                Name = clan.Name,
                LogoUrl = LogoOf(clan, owner),
                OwnerName = string.IsNullOrEmpty(owner.DisplayName) ? owner.UserName : owner.DisplayName,
                OwnerUserName = owner.UserName,
                Level = clan.Level,
                EnchantingLevel = enchanting,
                EnchantingMaxLevel = enchantingMax,
                Members = memberships.Count,
                PlayingNow = hidePlaying ? 0 : playing,
                PlayingHidden = hidePlaying,
                Founded = clan.Created,
                JoinPolicy = (RavenNest.Models.ClanJoinPolicy)clan.JoinPolicy,
            };
        }

        // as /clan shows it: the uploaded logo, an old logo address, or the owner's Twitch picture
        private string LogoOf(DataModels.Clan clan, DataModels.User owner)
        {
            if (clan.LogoImageId != null) return "/api/clan/image/" + clan.LogoImageId;
            if (!string.IsNullOrEmpty(clan.Logo)) return clan.Logo;
            return "/api/twitch/logo/" + owner.Id;
        }

        private static string NameOf(DataModels.Character character)
        {
            // a typed alias ("Cook") says something; the numbers most characters carry as their identifier do not
            var name = character.Name;
            if (!string.IsNullOrEmpty(character.Identifier) && !character.Identifier.All(char.IsDigit))
                name += " (" + character.Identifier + ")";
            if (character.CharacterIndex > 0)
                name += " #" + character.CharacterIndex;
            return name;
        }

        // the streams live right now, by the streamer's user id, with the streamer's name
        private Dictionary<Guid, string> GetLiveStreams()
        {
            var live = new Dictionary<Guid, string>();
            foreach (var session in gameData.GetActiveSessions())
            {
                var owner = gameData.GetUser(session.UserId);
                if (owner != null)
                    live[session.UserId] = owner.UserName;
            }

            return live;
        }

        // a character is locked to the session it joined, so the lock and the live sessions say where it plays
        private static string PlayingOn(DataModels.Character character, Dictionary<Guid, string> live) =>
            character.UserIdLock != null && live.TryGetValue(character.UserIdLock.Value, out var streamer) ? streamer : null;
    }

    public class PublicClanSummary
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string LogoUrl { get; set; }
        public string OwnerName { get; set; }
        public string OwnerUserName { get; set; }
        public int Level { get; set; }
        public int EnchantingLevel { get; set; }
        public int EnchantingMaxLevel { get; set; }
        public int Members { get; set; }
        public int PlayingNow { get; set; }

        /// <summary>The owner keeps who is playing to the clan (step 3).</summary>
        public bool PlayingHidden { get; set; }
        public DateTime Founded { get; set; }
        public RavenNest.Models.ClanJoinPolicy JoinPolicy { get; set; }

        public string JoinPolicyText => JoinPolicy switch
        {
            RavenNest.Models.ClanJoinPolicy.OnRequest => "On request",
            RavenNest.Models.ClanJoinPolicy.InviteOnly => "Invite only",
            _ => "Open",
        };

        /// <summary>The page's address: by name, which reads well and is what people share.</summary>
        public string Url => "/clans/" + Uri.EscapeDataString(Name ?? Id.ToString());
    }

    public class PublicClanPage
    {
        public PublicClanSummary Summary { get; set; }
        public double Experience { get; set; }
        public IReadOnlyList<PublicClanSkill> Skills { get; set; }
        public IReadOnlyList<PublicClanRank> Ranks { get; set; }
        public IReadOnlyList<PublicClanMember> Members { get; set; }

        /// <summary>The owner's stream name while they are live, otherwise null.</summary>
        public string OwnerLiveOn { get; set; }

        /// <summary>The visitor has a character in the clan (or owns it), so the page links to /clan.</summary>
        public bool VisitorIsMember { get; set; }
        public bool VisitorSignedIn { get; set; }

        // what the owner keeps to the clan (step 3); false for the clan's own members
        public bool MembersHidden { get; set; }
        public bool RanksHidden { get; set; }
        public bool ProgressHidden { get; set; }
        public bool ShowsRank { get; set; }
        public bool ShowsJoined { get; set; }
        public bool ShowsPlaying { get; set; }

        // the page the owner writes (step 4)
        public string About { get; set; }

        /// <summary>An admin hid the about text; only the clan's members see it, marked.</summary>
        public bool AboutHidden { get; set; }
        public string AccentColor { get; set; }
        public string BannerUrl { get; set; }
        public IReadOnlyList<PublicClanMember> TopMembers { get; set; }
        public IReadOnlyList<PublicClanPost> Posts { get; set; }
        public long VisitorCount { get; set; }
    }

    public class PublicClanPost
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string Author { get; set; }
        public bool AuthorIsOwner { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Edited { get; set; }
        public bool MembersOnly { get; set; }
        public bool Pinned { get; set; }
        public bool Hidden { get; set; }
    }

    public class PublicClanSkill
    {
        public string Name { get; set; }
        public int Level { get; set; }
        public double Experience { get; set; }
        public int MaxLevel { get; set; }
    }

    public class PublicClanRank
    {
        public string Name { get; set; }
        public int Level { get; set; }
        public IReadOnlyList<PublicClanMember> Members { get; set; }
    }

    public class PublicClanMember
    {
        public Guid CharacterId { get; set; }
        public string Name { get; set; }
        public string AvatarUrl { get; set; }
        public string Rank { get; set; }
        public int RankLevel { get; set; }
        public DateTime Joined { get; set; }
        public string PlayingOn { get; set; }
        public bool IsOwner { get; set; }
        public bool IsPlaying => !string.IsNullOrEmpty(PlayingOn);
    }
}
