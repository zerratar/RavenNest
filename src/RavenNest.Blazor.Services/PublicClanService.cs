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

        // the list is the same for everyone and costs a pass over every membership, so it is kept a minute
        private static readonly TimeSpan ListCacheDuration = TimeSpan.FromSeconds(60);
        private readonly object listLock = new object();
        private IReadOnlyList<PublicClanSummary> cachedList;
        private DateTime cachedListAt;

        public PublicClanService(
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider,
            GameData gameData)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
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
                    Joined = membership.Joined,
                    PlayingOn = PlayingOn(character, live),
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

            var session = GetSession();
            var visitorIsMember = session.Authenticated && session.UserId != Guid.Empty
                && (clan.UserId == session.UserId || members.Any(x => gameData.GetCharacter(x.CharacterId)?.UserId == session.UserId));

            var owner = gameData.GetUser(clan.UserId);
            return new PublicClanPage
            {
                Summary = summary,
                Experience = clan.Experience,
                Skills = skills,
                Ranks = ranks,
                Members = members,
                OwnerLiveOn = owner != null && live.ContainsKey(owner.Id) ? live[owner.Id] : null,
                VisitorIsMember = visitorIsMember,
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
                PlayingNow = playing,
                Founded = clan.Created,
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
        public DateTime Founded { get; set; }

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
