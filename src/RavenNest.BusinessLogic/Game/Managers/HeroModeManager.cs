using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// Hero mode's runs (the game's docs/hero-mode-design.md, 7d): each streamer's best wave run, kept as a user
    /// property ("hero_best": waves|kills|seconds|ticks), the leaderboard of every streamer's best, and the titles a
    /// run unlocks for the streamer at waves 10, 25 and 50 (streamer unlocks, worn with !title). The waves held count
/// for the stream's quests (QuestTracker.HeroWaves).
    /// </summary>
    public class HeroModeManager
    {
        public const string BestKey = "hero_best";
        /// <summary>The streamer's best run of the day's challenge: day|waves|kills|seconds|ticks|twists (day as yyyyMMdd,
        /// UTC; twists as the game names them, "Hunters,Blessed-steel").</summary>
        public const string DailyKey = "hero_daily";

        private static readonly (int Waves, string Title)[] Titles =
        {
            (10, "title_hero_10"),
            (25, "title_hero_25"),
            (50, "title_hero_50"),
        };

        // the game's price for an elite is 1500 coins; nothing in hero mode costs more than this
        private const long MostSpent = 10_000;
        private const int MinSecondsPerWave = 12; // the game's breather alone is 12 s, the first wave's count 8 s

        private readonly GameData gameData;
        private readonly object mutex = new object();
        private readonly Dictionary<Guid, DateTime> lastRun = new Dictionary<Guid, DateTime>(); // by session

        private readonly VillageDecorManager decorManager;

        public HeroModeManager(GameData gameData, VillageDecorManager decorManager)
        {
            this.gameData = gameData;
            this.decorManager = decorManager;
        }

        /// <summary>A run in the streamer's session ended: the best kept if it is better, titles unlocked.</summary>
        public HeroRunResult Record(SessionToken token, int waves, int kills, int seconds)
        {
            var session = token != null ? gameData.GetSession(token.SessionId) : null;
            if (session == null) return new HeroRunResult { Error = "notinsession" };
            waves = Math.Clamp(waves, 0, 10000);
            kills = Math.Max(0, kills);
            seconds = Math.Max(0, seconds);

            lock (mutex)
            {
                // the game sends the numbers; a run cannot have lasted longer than since this session's last one,
                // nor held a wave in under MinSecondsPerWave (a wave's fight plus the breather), so a made up run
                // cannot top the board or unlock the titles
                var now = DateTime.UtcNow;
                if (lastRun.TryGetValue(token.SessionId, out var previous)) seconds = Math.Min(seconds, (int)Math.Ceiling((now - previous).TotalSeconds) + 60);
                lastRun[token.SessionId] = now;
                waves = Math.Min(waves, seconds / MinSecondsPerWave);

                var user = gameData.GetUser(session.UserId);
                var best = Parse(gameData.GetUserProperty(session.UserId, BestKey), user);
                var run = new HeroRunRecord { Streamer = Name(user), Waves = waves, Kills = kills, Seconds = seconds, Reached = DateTime.UtcNow };
                var newBest = best == null || Better(run, best);
                if (newBest)
                {
                    gameData.SetUserProperty(session.UserId, BestKey, Format(run));
                    best = run;
                }

                var titles = new List<string>();
                foreach (var (need, title) in Titles)
                {
                    if (waves < need || gameData.GetStreamerUnlocks(session.UserId).Any(u => u.Key == title)) continue;
                    gameData.Add(new StreamerUnlock { Id = Guid.NewGuid(), StreamerUserId = session.UserId, Key = title, Source = "hero", Created = DateTime.UtcNow });
                    titles.Add(title.Substring("title_".Length));
                }

                // the village's decor for the same marks: a banner, the goblins' war drum, the hero's statue
                var decor = new List<string>();
                foreach (var (need, _) in Titles)
                {
                    if (waves < need) continue;
                    var key = decorManager.UnlockForQuest(session.Id, "hero" + need);
                    if (key != null) decor.Add(key);
                }

                // the stream's quests count the waves held (the streamer's character in the session)
                var character = gameData.GetActiveSessionCharacters(session).FirstOrDefault(c => c.UserId == session.UserId);
                if (character != null) QuestTracker.HeroWaves(character, waves);

                var rank = 1 + Board().Count(r => Better(r, best));
                return new HeroRunResult { Ok = true, Best = best, NewBest = newBest, NewTitles = titles.ToArray(), NewDecor = decor.ToArray(), Rank = rank };
            }
        }

        /// <summary>
        /// A run of the day's challenge (the game's WaveRun.Daily: the same waves and twists for every streamer that UTC
        /// day) ended: the streamer's best of that day kept if better, its place among the day's. Not the all-time board,
        /// nor its titles and decor (the twists make some days easier); the waves count for the stream's quests.
        /// </summary>
        public HeroRunResult RecordDaily(SessionToken token, int day, int waves, int kills, int seconds, string twists = null)
        {
            twists = CleanTwists(twists);
            var session = token != null ? gameData.GetSession(token.SessionId) : null;
            if (session == null) return new HeroRunResult { Error = "notinsession" };
            // today's, or yesterday's for a run that went past midnight
            var today = Day(DateTime.UtcNow);
            if (day != today && day != Day(DateTime.UtcNow.AddDays(-1))) return new HeroRunResult { Error = "day" };
            waves = Math.Clamp(waves, 0, 10000);
            kills = Math.Max(0, kills);
            seconds = Math.Max(0, seconds);

            lock (mutex)
            {
                var now = DateTime.UtcNow;
                if (lastRun.TryGetValue(token.SessionId, out var previous)) seconds = Math.Min(seconds, (int)Math.Ceiling((now - previous).TotalSeconds) + 60);
                lastRun[token.SessionId] = now;
                waves = Math.Min(waves, seconds / MinSecondsPerWave);

                var user = gameData.GetUser(session.UserId);
                var (bestDay, best, _) = ParseDaily(gameData.GetUserProperty(session.UserId, DailyKey), user);
                var run = new HeroRunRecord { Streamer = Name(user), Waves = waves, Kills = kills, Seconds = seconds, Reached = now };
                // a new day starts afresh; a run from yesterday never replaces today's
                var newBest = best == null || bestDay < day || (bestDay == day && Better(run, best));
                if (bestDay > day) newBest = false;
                if (newBest)
                {
                    gameData.SetUserProperty(session.UserId, DailyKey, day.ToString(CultureInfo.InvariantCulture) + "|" + Format(run) + "|" + twists);
                    best = run;
                    bestDay = day;
                }

                var character = gameData.GetActiveSessionCharacters(session).FirstOrDefault(c => c.UserId == session.UserId);
                if (character != null) QuestTracker.HeroWaves(character, waves);

                var rank = bestDay == day ? 1 + DailyBoard(day).Count(r => Better(r, best)) : 0;
                return new HeroRunResult { Ok = true, Best = bestDay == day ? best : run, NewBest = newBest, NewTitles = Array.Empty<string>(), NewDecor = Array.Empty<string>(), Rank = rank };
            }
        }

        /// <summary>The best runs of today's challenge, the best first, and the day's twists as the runs sent them (none
        /// until somebody played it: the game works them out from the date).</summary>
        public HeroLeaderboard DailyLeaderboard(int count)
        {
            var today = Day(DateTime.UtcNow);
            string twists = null;
            var entries = new List<HeroRunRecord>();
            foreach (var p in gameData.GetUserPropertiesWithKey(DailyKey))
            {
                var (d, r, t) = ParseDaily(p.Value, gameData.GetUser(p.UserId));
                if (r == null || d != today) continue;
                entries.Add(r);
                if (string.IsNullOrEmpty(twists) && !string.IsNullOrEmpty(t)) twists = t;
            }
            return new HeroLeaderboard
            {
                Day = today,
                Twists = string.IsNullOrEmpty(twists) ? Array.Empty<string>() : twists.Split(','),
                Entries = entries.OrderByDescending(r => r.Waves).ThenByDescending(r => r.Kills).ThenBy(r => r.Seconds).Take(Math.Clamp(count, 1, 100)).ToArray(),
            };
        }

        private IEnumerable<HeroRunRecord> DailyBoard(int day)
        {
            foreach (var p in gameData.GetUserPropertiesWithKey(DailyKey))
            {
                var (d, r, _) = ParseDaily(p.Value, gameData.GetUser(p.UserId));
                if (r != null && d == day) yield return r;
            }
        }

        /// <summary>Twists as the game sends them: letters, '-' before Blessed's boon, ',' between; anything else dropped.</summary>
        private static string CleanTwists(string twists)
        {
            if (string.IsNullOrEmpty(twists)) return string.Empty;
            var clean = new string(twists.Where(c => char.IsLetter(c) || c == ',' || c == '-').ToArray());
            return clean.Length > 80 ? clean.Substring(0, 80) : clean;
        }

        private static int Day(DateTime utc) => utc.Year * 10000 + utc.Month * 100 + utc.Day;

        private static (int Day, HeroRunRecord Run, string Twists) ParseDaily(string value, User user)
        {
            if (string.IsNullOrEmpty(value)) return (0, null, null);
            var cut = value.IndexOf('|');
            if (cut <= 0 || !int.TryParse(value.Substring(0, cut), out var day)) return (0, null, null);
            var run = Parse(value.Substring(cut + 1), user);
            var parts = value.Split('|');
            return run == null ? (0, null, null) : (day, run, parts.Length > 5 ? parts[5] : null);
        }

        /// <summary>
        /// Coins a character playing in the calling stream pays in hero mode (the game's !hero elite, an elite enemy sent
        /// at the hero that drops better loot). Returns the coins left, or -1 when they had too few.
        /// </summary>
        public long Spend(SessionToken session, Guid characterId, long coins)
        {
            if (coins <= 0 || coins > MostSpent) return -1;
            var character = gameData.GetCharacter(characterId);
            if (character == null || session == null || character.UserIdLock != session.UserId) return -1;
            var resources = gameData.GetResources(character);
            if (resources == null || resources.Coins < coins) return -1;
            resources.Coins -= coins;
            gameData.Touch();
            return (long)resources.Coins;
        }

        /// <summary>The website's quest page: a streamer's best run and its place among every streamer's (0 for none).</summary>
        public (HeroRunRecord Best, int Rank) BestOf(Guid userId)
        {
            var best = Parse(gameData.GetUserProperty(userId, BestKey), gameData.GetUser(userId));
            if (best == null) return (null, 0);
            return (best, 1 + Board().Count(r => Better(r, best)));
        }

        /// <summary>The best runs of every streamer, the best first.</summary>
        public HeroLeaderboard Leaderboard(int count)
        {
            return new HeroLeaderboard { Entries = Board().OrderByDescending(r => r.Waves).ThenByDescending(r => r.Kills).ThenBy(r => r.Seconds).Take(Math.Clamp(count, 1, 100)).ToArray() };
        }

        private IEnumerable<HeroRunRecord> Board()
        {
            foreach (var p in gameData.GetUserPropertiesWithKey(BestKey))
            {
                var r = Parse(p.Value, gameData.GetUser(p.UserId));
                if (r != null) yield return r;
            }
        }

        private static bool Better(HeroRunRecord a, HeroRunRecord b)
        {
            if (a.Waves != b.Waves) return a.Waves > b.Waves;
            if (a.Kills != b.Kills) return a.Kills > b.Kills;
            return a.Seconds < b.Seconds;
        }

        private static string Name(User user) => user == null ? "?" : !string.IsNullOrEmpty(user.DisplayName) ? user.DisplayName : user.UserName;

        private static string Format(HeroRunRecord r) => string.Join("|", r.Waves, r.Kills, r.Seconds, r.Reached.Ticks.ToString(CultureInfo.InvariantCulture));

        private static HeroRunRecord Parse(string value, User user)
        {
            if (string.IsNullOrEmpty(value)) return null;
            var parts = value.Split('|');
            if (parts.Length < 4 || !int.TryParse(parts[0], out var waves) || !int.TryParse(parts[1], out var kills) || !int.TryParse(parts[2], out var seconds) || !long.TryParse(parts[3], out var ticks)) return null;
            return new HeroRunRecord { Streamer = Name(user), Waves = waves, Kills = kills, Seconds = seconds, Reached = new DateTime(ticks, DateTimeKind.Utc) };
        }
    }
}
