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

        private static readonly (int Waves, string Title)[] Titles =
        {
            (10, "title_hero_10"),
            (25, "title_hero_25"),
            (50, "title_hero_50"),
        };

        private readonly GameData gameData;
        private readonly object mutex = new object();

        public HeroModeManager(GameData gameData)
        {
            this.gameData = gameData;
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

                // the stream's quests count the waves held (the streamer's character in the session)
                var character = gameData.GetActiveSessionCharacters(session).FirstOrDefault(c => c.UserId == session.UserId);
                if (character != null) QuestTracker.HeroWaves(character, waves);

                var rank = 1 + Board().Count(r => Better(r, best));
                return new HeroRunResult { Ok = true, Best = best, NewBest = newBest, NewTitles = titles.ToArray(), Rank = rank };
            }
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
