using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    ///     A clan's public page beyond its stats (the game's docs/clan-pages-design.md, steps 3 and 4): what the public
    ///     may see, the about text, the accent colour, the Top 8, news posts, reports for admins and the visitor
    ///     counter. Who may change what is checked by the callers (ClanService), as for the rest of the clan.
    /// </summary>
    public class ClanPageManager
    {
        public const int MaxAboutLength = 2000;
        public const int MaxTitleLength = 100;
        public const int MaxBodyLength = 4000;
        public const int MaxReasonLength = 300;
        public const int TopMemberCount = 8;
        public const int MaxPostsPerDay = 10;

        private static readonly Regex Colour = new Regex("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

        private readonly GameData gameData;
        private readonly object mutex = new object();

        // who was counted today, per clan: a visitor counts once a day, and a restart forgets (it is a counter, not a ledger)
        private readonly ConcurrentDictionary<Guid, HashSet<string>> visitsToday = new();
        private DateTime visitsDay = DateTime.UtcNow.Date;

        public ClanPageManager(GameData gameData)
        {
            this.gameData = gameData;
        }

        /// <summary>The clan's settings, or null when the owner has changed nothing (everything shown, no text).</summary>
        public ClanPageSettings Get(Guid clanId) => gameData.GetClanPageSettings(clanId);

        private ClanPageSettings GetOrCreate(Guid clanId)
        {
            lock (mutex)
            {
                var settings = gameData.GetClanPageSettings(clanId);
                if (settings != null)
                    return settings;

                settings = new ClanPageSettings { Id = Guid.NewGuid(), ClanId = clanId };
                gameData.Add(settings);
                return settings;
            }
        }

        public void SetPrivacy(Guid clanId, bool hideMembers, bool hidePlaying, bool hideJoined, bool hideRanks, bool hideProgress)
        {
            var settings = GetOrCreate(clanId);
            settings.HideMembers = hideMembers;
            settings.HidePlaying = hidePlaying;
            settings.HideJoined = hideJoined;
            settings.HideRanks = hideRanks;
            settings.HideProgress = hideProgress;
            settings.Updated = DateTime.UtcNow;
        }

        /// <summary>The about text, trimmed to <see cref="MaxAboutLength"/>; a changed text shows again if an admin had hidden the old one.</summary>
        public void SetAbout(Guid clanId, string about)
        {
            about = Clean(about, MaxAboutLength);
            var settings = GetOrCreate(clanId);
            if (settings.About != about)
                settings.AboutHidden = false;
            settings.About = about;
            settings.Updated = DateTime.UtcNow;
        }

        /// <summary>#RRGGBB, or empty for the site's gold. False for anything else.</summary>
        public bool SetAccentColor(Guid clanId, string color)
        {
            color = (color ?? "").Trim();
            if (color.Length > 0 && !Colour.IsMatch(color))
                return false;

            var settings = GetOrCreate(clanId);
            settings.AccentColor = color.Length > 0 ? color.ToUpperInvariant() : null;
            settings.Updated = DateTime.UtcNow;
            return true;
        }

        /// <summary>Up to eight members of the clan, in order; anyone not in the clan is left out.</summary>
        public void SetTopMembers(Guid clanId, IEnumerable<Guid> characterIds)
        {
            var members = new HashSet<Guid>(gameData.GetClanMemberships(clanId).Select(x => x.CharacterId));
            var ids = (characterIds ?? Enumerable.Empty<Guid>()).Where(members.Contains).Distinct().Take(TopMemberCount).ToList();
            var settings = GetOrCreate(clanId);
            settings.TopMembers = ids.Count > 0 ? string.Join(",", ids) : null;
            settings.Updated = DateTime.UtcNow;
        }

        /// <summary>The Top 8 that are still in the clan, in the owner's order.</summary>
        public IReadOnlyList<Guid> GetTopMembers(Guid clanId)
        {
            var text = Get(clanId)?.TopMembers;
            if (string.IsNullOrEmpty(text))
                return Array.Empty<Guid>();

            var members = new HashSet<Guid>(gameData.GetClanMemberships(clanId).Select(x => x.CharacterId));
            return text.Split(',')
                .Select(x => Guid.TryParse(x, out var id) ? id : Guid.Empty)
                .Where(members.Contains)
                .ToList();
        }

        #region News
        /// <summary>The clan's posts, pinned first, then newest; members only and hidden posts when asked for.</summary>
        public IReadOnlyList<ClanPost> GetPosts(Guid clanId, bool includeMembersOnly, bool includeHidden) =>
            gameData.GetClanPosts(clanId)
                .Where(x => !x.Deleted && (includeMembersOnly || !x.MembersOnly) && (includeHidden || !x.Hidden))
                .OrderByDescending(x => x.Pinned)
                .ThenByDescending(x => x.Created)
                .ToList();

        public ClanPost CreatePost(Guid clanId, Guid authorUserId, Guid authorCharacterId, string title, string body, bool membersOnly, bool pinned, out string error)
        {
            error = null;
            title = Clean(title, MaxTitleLength);
            body = Clean(body, MaxBodyLength);
            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(body))
            {
                error = "A post needs a title and some text.";
                return null;
            }

            var since = DateTime.UtcNow.AddDays(-1);
            if (gameData.GetClanPosts(clanId).Count(x => x.Created > since) >= MaxPostsPerDay)
            {
                error = "That is " + MaxPostsPerDay + " posts today already. Edit one, or post again tomorrow.";
                return null;
            }

            var post = new ClanPost
            {
                Id = Guid.NewGuid(),
                ClanId = clanId,
                AuthorUserId = authorUserId,
                AuthorCharacterId = authorCharacterId,
                Title = title,
                Body = body,
                MembersOnly = membersOnly,
                Pinned = pinned,
                Created = DateTime.UtcNow,
            };
            gameData.Add(post);
            return post;
        }

        /// <summary>A post of this clan, with new text; an edited post shows again if an admin had hidden it, so it is looked at again on a new report.</summary>
        public bool UpdatePost(Guid clanId, Guid postId, string title, string body, bool membersOnly, bool pinned, out string error)
        {
            error = null;
            var post = gameData.GetClanPost(postId);
            if (post == null || post.ClanId != clanId || post.Deleted)
            {
                error = "That post is gone.";
                return false;
            }

            title = Clean(title, MaxTitleLength);
            body = Clean(body, MaxBodyLength);
            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(body))
            {
                error = "A post needs a title and some text.";
                return false;
            }

            if (post.Title != title || post.Body != body)
            {
                post.Edited = DateTime.UtcNow;
                post.Hidden = false;
            }

            post.Title = title;
            post.Body = body;
            post.MembersOnly = membersOnly;
            post.Pinned = pinned;
            return true;
        }

        public bool DeletePost(Guid clanId, Guid postId)
        {
            var post = gameData.GetClanPost(postId);
            if (post == null || post.ClanId != clanId)
                return false;

            post.Deleted = true;
            return true;
        }
        #endregion

        #region Reports
        /// <summary>Reports a post (or, with an empty post id, the about text). One open report per person and thing.</summary>
        public bool Report(Guid clanId, Guid postId, Guid reporterUserId, string reason, out string error)
        {
            error = null;
            if (postId != Guid.Empty)
            {
                var post = gameData.GetClanPost(postId);
                if (post == null || post.ClanId != clanId || post.Deleted)
                {
                    error = "That post is gone.";
                    return false;
                }
            }

            if (gameData.GetClanPostReports().Any(x => x.State == 0 && x.ClanId == clanId && x.PostId == postId && x.ReporterUserId == reporterUserId))
                return true;

            gameData.Add(new ClanPostReport
            {
                Id = Guid.NewGuid(),
                ClanId = clanId,
                PostId = postId,
                ReporterUserId = reporterUserId,
                Reason = Clean(reason, MaxReasonLength),
                Created = DateTime.UtcNow,
            });
            return true;
        }

        public IReadOnlyList<ClanPostReport> GetOpenReports() =>
            gameData.GetClanPostReports().Where(x => x.State == 0).OrderBy(x => x.Created).ToList();

        public int GetOpenReportCount() => gameData.GetClanPostReports().Count(x => x.State == 0);

        /// <summary>
        ///     An admin's answer: hide the post (or the about text) and close every open report on it, or dismiss the
        ///     reports on it as nothing wrong.
        /// </summary>
        public bool Resolve(Guid reportId, Guid adminUserId, bool hide)
        {
            var report = gameData.GetClanPostReport(reportId);
            if (report == null || report.State != 0)
                return false;

            if (hide)
            {
                if (report.PostId != Guid.Empty)
                {
                    var post = gameData.GetClanPost(report.PostId);
                    if (post != null) post.Hidden = true;
                }
                else
                {
                    var settings = Get(report.ClanId);
                    if (settings != null) settings.AboutHidden = true;
                }
            }

            foreach (var same in gameData.GetClanPostReports().Where(x => x.State == 0 && x.ClanId == report.ClanId && x.PostId == report.PostId))
            {
                same.State = hide ? 1 : 2;
                same.ResolvedBy = adminUserId;
                same.Resolved = DateTime.UtcNow;
            }

            return true;
        }
        #endregion

        /// <summary>Counts a visit once a day per visitor (a user id, or a hash of the address); returns the count.</summary>
        public long CountVisit(Guid clanId, string visitorKey)
        {
            if (string.IsNullOrEmpty(visitorKey))
                return Get(clanId)?.VisitorCount ?? 0;

            var today = DateTime.UtcNow.Date;
            if (today != visitsDay)
            {
                visitsDay = today;
                visitsToday.Clear();
            }

            var seen = visitsToday.GetOrAdd(clanId, _ => new HashSet<string>());
            lock (seen)
            {
                if (!seen.Add(visitorKey))
                    return Get(clanId)?.VisitorCount ?? 0;
            }

            var settings = GetOrCreate(clanId);
            lock (mutex)
            {
                settings.VisitorCount++;
                return settings.VisitorCount;
            }
        }

        // trimmed, newlines as \n, nothing past the limit, and no control characters but line breaks
        private static string Clean(string text, int max)
        {
            text = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
            text = new string(text.Where(c => c == '\n' || !char.IsControl(c)).ToArray()).Trim();
            return text.Length > max ? text.Substring(0, max).TrimEnd() : text;
        }
    }
}
