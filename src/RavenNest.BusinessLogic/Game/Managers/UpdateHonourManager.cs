using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// Honours for streams that update quickly (the user, 2026-11-04: a carrot for the background download and the
    /// automatic install). When a stream starts on the newest release: the first stream in the world on that version
    /// keeps the record for good (a Trailblazer), and every stream within ten minutes of the release going up on GitHub
    /// is an Early Bird. Both count up over the releases into titles for the streamer's own characters, as the story
    /// lines' titles do. The website's download page lists who was first on each version.
    /// </summary>
    /// <remarks>
    /// Kept as streamer unlocks: "update_first_0.9.7.0a" and "update_early_0.9.7.0a" (the release's version), and the
    /// titles as "title_trailblazer_1" and "title_earlybird_1". The counts are how many of those a streamer has. Only
    /// the server can tell when a stream started and what it runs, so neither can be claimed early. Game administrators
    /// can be Early Birds but never take a version's first place: they have the build before anyone else.
    /// </remarks>
    public class UpdateHonourManager
    {
        public const string FirstPrefix = "update_first_";
        public const string EarlyPrefix = "update_early_";

        /// <summary>How soon after the release a stream must start on it to be an Early Bird.</summary>
        public static readonly TimeSpan EarlyWindow = TimeSpan.FromMinutes(10);

        /// <summary>Releases from before the honours existed do not count: nobody raced for them.</summary>
        private static readonly DateTime CountsFrom = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc);

        private static readonly (int Count, string Title)[] TrailblazerTitles = { (1, "title_trailblazer_1"), (3, "title_trailblazer_2"), (10, "title_trailblazer_3") };
        private static readonly (int Count, string Title)[] EarlyBirdTitles = { (1, "title_earlybird_1"), (5, "title_earlybird_2"), (20, "title_earlybird_3") };

        private readonly GameData gameData;
        private readonly ILogger<UpdateHonourManager> logger;
        private readonly object mutex = new object();

        public UpdateHonourManager(GameData gameData, ILogger<UpdateHonourManager> logger)
        {
            this.gameData = gameData;
            this.logger = logger;
        }

        /// <summary>A stream started on the given version: any honour it earned, told to the game.</summary>
        public void SessionStarted(DataModels.GameSession session, DataModels.User user, string clientVersion)
        {
            try
            {
                var release = GameReleaseWatcher.Latest;
                if (session == null || user == null || release == null || string.IsNullOrEmpty(clientVersion)) return;
                if (!string.Equals(release.Version, clientVersion, StringComparison.OrdinalIgnoreCase)) return;
                if (release.Published < CountsFrom) return;

                var now = DateTime.UtcNow;
                var since = now - release.Published;
                var honour = new UpdateHonour { Version = release.Version, Minutes = Math.Max(0, (int)Math.Floor(since.TotalMinutes)) };
                var titles = new List<string>();

                lock (mutex)
                {
                    var mine = gameData.GetStreamerUnlocks(user.Id);
                    if (since >= TimeSpan.Zero && since <= EarlyWindow && !mine.Any(u => u.Key == EarlyPrefix + release.Version))
                    {
                        Add(user.Id, EarlyPrefix + release.Version, now);
                        honour.Early = true;
                        titles.AddRange(NewTitles(user.Id, EarlyPrefix, EarlyBirdTitles, now));
                    }

                    var firstKey = FirstPrefix + release.Version;
                    if (!user.IsAdmin.GetValueOrDefault() && !gameData.GetAllStreamerUnlocks().Any(u => u.Key == firstKey))
                    {
                        Add(user.Id, firstKey, now);
                        honour.First = true;
                        titles.AddRange(NewTitles(user.Id, FirstPrefix, TrailblazerTitles, now));
                    }

                    honour.EarlyCount = gameData.GetStreamerUnlocks(user.Id).Count(u => u.Key.StartsWith(EarlyPrefix, StringComparison.Ordinal));
                    honour.FirstCount = gameData.GetStreamerUnlocks(user.Id).Count(u => u.Key.StartsWith(FirstPrefix, StringComparison.Ordinal));
                }

                if (!honour.Early && !honour.First) return;
                honour.NewTitles = titles.ToArray();
                logger.LogInformation((user.DisplayName ?? user.UserName) + " updated to " + release.Version + " after " + honour.Minutes + " min" +
                    (honour.First ? ", the first stream on it" : "") + (honour.Early ? ", an Early Bird" : "") + ".");
                gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.UpdateHonour, session, honour));
            }
            catch (Exception exc)
            {
                logger.LogError("Update honours failed for " + user?.UserName + ": " + exc);
            }
        }

        /// <summary>The website: each version's first stream, newest first, with how many Early Birds it had.</summary>
        public IReadOnlyList<UpdateHonourRecord> GetFirsts(int max = 50)
        {
            var unlocks = gameData.GetAllStreamerUnlocks();
            var early = unlocks.Where(u => u.Key.StartsWith(EarlyPrefix, StringComparison.Ordinal))
                .GroupBy(u => u.Key.Substring(EarlyPrefix.Length)).ToDictionary(g => g.Key, g => g.Count());
            return unlocks.Where(u => u.Key.StartsWith(FirstPrefix, StringComparison.Ordinal))
                .OrderByDescending(u => u.Created)
                .Take(max)
                .Select(u =>
                {
                    var version = u.Key.Substring(FirstPrefix.Length);
                    var streamer = gameData.GetUser(u.StreamerUserId);
                    return new UpdateHonourRecord
                    {
                        Version = version,
                        Streamer = streamer == null ? "?" : !string.IsNullOrEmpty(streamer.DisplayName) ? streamer.DisplayName : streamer.UserName,
                        StreamerLogin = streamer?.UserName,
                        Reached = u.Created,
                        Minutes = ParseMinutes(u.Source),
                        EarlyBirds = early.TryGetValue(version, out var n) ? n : 0,
                    };
                })
                .ToList();
        }

        /// <summary>A streamer's honours: how many first places and Early Birds.</summary>
        public (int Firsts, int EarlyBirds) GetCounts(Guid userId)
        {
            var mine = gameData.GetStreamerUnlocks(userId);
            return (mine.Count(u => u.Key.StartsWith(FirstPrefix, StringComparison.Ordinal)), mine.Count(u => u.Key.StartsWith(EarlyPrefix, StringComparison.Ordinal)));
        }

        private void Add(Guid userId, string key, DateTime now)
        {
            // the minutes after the release go in the source, for the website's list
            var release = GameReleaseWatcher.Latest;
            var minutes = release != null ? Math.Max(0, (int)Math.Floor((now - release.Published).TotalMinutes)) : 0;
            gameData.Add(new StreamerUnlock { Id = Guid.NewGuid(), StreamerUserId = userId, Key = key, Source = "update:" + minutes, Created = now });
        }

        private IEnumerable<string> NewTitles(Guid userId, string prefix, (int Count, string Title)[] tiers, DateTime now)
        {
            var mine = gameData.GetStreamerUnlocks(userId);
            var count = mine.Count(u => u.Key.StartsWith(prefix, StringComparison.Ordinal));
            var earned = new List<string>();
            foreach (var (need, title) in tiers)
            {
                if (count < need || mine.Any(u => u.Key == title)) continue;
                gameData.Add(new StreamerUnlock { Id = Guid.NewGuid(), StreamerUserId = userId, Key = title, Source = "update", Created = now });
                earned.Add(title.Substring("title_".Length));
            }
            return earned;
        }

        private static int? ParseMinutes(string source) =>
            source != null && source.StartsWith("update:", StringComparison.Ordinal) && int.TryParse(source.Substring(7), out var m) ? m : (int?)null;
    }

    /// <summary>One version's first stream, for the website.</summary>
    public class UpdateHonourRecord
    {
        public string Version { get; set; }
        public string Streamer { get; set; }
        public string StreamerLogin { get; set; }
        public DateTime Reached { get; set; }
        public int? Minutes { get; set; }
        public int EarlyBirds { get; set; }
    }
}
