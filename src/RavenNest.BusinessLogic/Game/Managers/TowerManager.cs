using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RavenNest.BusinessLogic.Data;
using RavenNest.Combat;
using RavenNest.DataModels;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    ///     The tower on the server (the game's docs/tower-design.md): which floor each stream is on, its quest progress,
    ///     boss attempts, clears and their rewards. Progress belongs to the stream (the streamer's user), like the village:
    ///     everyone playing there climbs together, and a floor once cleared stays cleared. The floors are
    ///     RavenNest.Combat.Tower's, shared with the game, which reports what happens on them.
    /// </summary>
    public class TowerManager
    {
        /// <summary>Kills one report may add to a quest: a game reports every few seconds, so more is a mistake.</summary>
        private const int MaxKillsPerReport = 500;

        /// <summary>Coins per floor number for each player in a won attempt; twice that for the world's first clear.</summary>
        private const long CoinsPerFloor = 250;

        /// <summary>Village coins per floor number when a floor is cleared.</summary>
        private const long VillageCoinsPerFloor = 1000;

        private readonly GameData gameData;
        private readonly GameManager gameManager;
        private readonly object mutex = new object();
        private readonly ConcurrentDictionary<Guid, DateTime> lastClear = new ConcurrentDictionary<Guid, DateTime>();

        public TowerManager(GameData gameData, GameManager gameManager)
        {
            this.gameData = gameData;
            this.gameManager = gameManager;
        }

        public TowerState GetState(SessionToken token)
        {
            var session = gameData.GetSession(token.SessionId);
            return session != null ? State(session.UserId) : null;
        }

        /// <summary>
        ///     Kills on the stream's current floor, as deltas in the quests' text form ("defeat-hall-goblin:3;..."). Kills on
        ///     any other floor (an old floor, a stale report) count for nothing.
        /// </summary>
        public TowerState AddProgress(SessionToken token, int floor, string counts)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null) return null;
            lock (mutex)
            {
                var tower = GetOrCreate(session.UserId);
                var definition = Tower.Get(tower.Floor);
                if (definition == null || floor != tower.Floor) return State(session.UserId);

                var progress = Tower.ParseProgress(definition, tower.Progress);
                var added = Tower.ParseProgress(definition, null);
                foreach (var part in (counts ?? "").Split(';'))
                {
                    var colon = part.IndexOf(':');
                    if (colon <= 0 || !int.TryParse(part.Substring(colon + 1), out var n) || n <= 0) continue;
                    var id = part.Substring(0, colon).Trim();
                    if (!added.ContainsKey(id)) continue;
                    added[id] = Math.Min(MaxKillsPerReport, n);
                }

                var changed = false;
                foreach (var quest in definition.Quests)
                {
                    var before = progress[quest.Id];
                    var after = Math.Min(quest.Amount, before + added[quest.Id]);
                    if (after != before) { progress[quest.Id] = after; changed = true; }
                }
                if (changed)
                {
                    tower.Progress = Tower.FormatProgress(progress);
                    tower.Updated = DateTime.UtcNow;
                    gameData.Touch();
                }
                return State(session.UserId);
            }
        }

        /// <summary>
        ///     Kills per character and kind on any tower floor, for the characters' own quests (QuestTracker.TowerKills):
        ///     characters of this session only, each count capped like a floor report.
        /// </summary>
        public void AddCharacterKills(SessionToken token, TowerKillReport report)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null || report == null) return;
            var characters = gameData.GetActiveSessionCharacters(session);
            foreach (var k in (report.Kills ?? Array.Empty<TowerCharacterKills>()).Take(1000))
            {
                if (k == null || k.Count <= 0 || string.IsNullOrEmpty(k.Kind)) continue;
                var character = characters.FirstOrDefault(c => c.Id == k.CharacterId);
                if (character == null) continue;
                QuestTracker.TowerKills(character, k.Kind, Math.Min(MaxKillsPerReport, k.Count));
            }

            // knocked out by a boss: only the stream's own floor counts (Nibbles is floor 1's, for the hidden "nibbled")
            var floor = GetOrCreate(session.UserId).Floor;
            foreach (var f in (report.Falls ?? Array.Empty<TowerCharacterFall>()).Take(200))
            {
                if (f == null || f.Floor != floor) continue;
                var character = characters.FirstOrDefault(c => c.Id == f.CharacterId);
                if (character != null) QuestTracker.TowerFell(character, f.Floor);
            }
        }

        /// <summary>A boss attempt starts on the stream's floor: counted, if the floor's quests are done.</summary>
        public TowerState BossStarted(SessionToken token, int floor)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null) return null;
            lock (mutex)
            {
                var tower = GetOrCreate(session.UserId);
                var definition = Tower.Get(tower.Floor);
                if (definition != null && floor == tower.Floor && Tower.QuestsDone(definition, Tower.ParseProgress(definition, tower.Progress)))
                {
                    tower.Attempts++;
                    tower.Updated = DateTime.UtcNow;
                    gameData.Touch();
                }
                return State(session.UserId);
            }
        }

        /// <summary>
        ///     The stream beat its floor's boss: the floor is cleared, the next one opens, and everyone who took part (in the
        ///     session) gets coins and a chest. Refused when it is not the stream's floor or its quests are not done.
        /// </summary>
        public TowerClearResult BossWon(SessionToken token, int floor, Guid[] characterIds)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null) return new TowerClearResult { Message = "No game session." };
            if (lastClear.TryGetValue(session.Id, out var last) && DateTime.UtcNow - last < TimeSpan.FromSeconds(30))
                return new TowerClearResult { Message = "A floor was cleared moments ago.", State = State(session.UserId) };

            TowerClear clear;
            bool worldFirst;
            lock (mutex)
            {
                var tower = GetOrCreate(session.UserId);
                var definition = Tower.Get(tower.Floor);
                if (definition == null || floor != tower.Floor)
                    return new TowerClearResult { Message = "That is not the floor the stream is on.", State = State(session.UserId) };
                if (!Tower.QuestsDone(definition, Tower.ParseProgress(definition, tower.Progress)))
                    return new TowerClearResult { Message = "The floor's quests are not done.", State = State(session.UserId) };

                worldFirst = !gameData.GetTowerClears().Any(x => x.Floor == floor);
                var characters = gameData.GetActiveSessionCharacters(session);
                var players = (characterIds ?? Array.Empty<Guid>()).Distinct().Count(id => characters.Any(c => c.Id == id));
                clear = new TowerClear
                {
                    Id = Guid.NewGuid(),
                    UserId = session.UserId,
                    Floor = floor,
                    Cleared = DateTime.UtcNow,
                    Players = players,
                    Attempts = Math.Max(1, tower.Attempts),
                };
                gameData.Add(clear);

                tower.Floor = floor + 1;
                tower.Progress = null;
                tower.Attempts = 0;
                tower.Updated = DateTime.UtcNow;
                lastClear[session.Id] = DateTime.UtcNow;
            }

            // the rewards: coins for each player, a chest from the dungeon table by the floor (floor 10's tier is epic),
            // two items on every fifth floor; the village gets its share
            var ids = (characterIds ?? Array.Empty<Guid>()).Distinct().ToArray();
            var coins = CoinsPerFloor * floor * (worldFirst ? 2 : 1);
            var sessionCharacters = gameData.GetActiveSessionCharacters(session);
            foreach (var id in ids)
            {
                var character = sessionCharacters.FirstOrDefault(c => c.Id == id);
                if (character == null) continue;
                var resources = gameData.GetResources(character);
                if (resources != null) resources.Coins += coins;
            }
            var village = gameData.GetVillageBySession(session);
            var villageResources = village != null ? gameData.GetResources(village.ResourcesId) : null;
            if (villageResources != null) villageResources.Coins += VillageCoinsPerFloor * floor;

            // the characters' own quests count the clear (Mossgate's chains)
            foreach (var id in ids)
            {
                var character = sessionCharacters.FirstOrDefault(c => c.Id == id);
                if (character != null) QuestTracker.TowerCleared(character, floor);
            }

            var tier = Math.Min(4, (floor - 1) / 3);
            var rewards = gameManager.RollTowerChest(session, tier, ids, floor % 5 == 0 ? 2 : 1);
            gameData.Touch();

            return new TowerClearResult
            {
                Success = true,
                Floor = floor,
                WorldFirst = worldFirst,
                Coins = coins,
                Rewards = rewards,
                State = State(session.UserId),
            };
        }

        /// <summary>The highest floor each stream has cleared, for the website: best first, earlier first on a tie.</summary>
        public IReadOnlyList<(Guid UserId, string Name, int Floor, DateTime Cleared)> Leaderboard(int count)
        {
            return gameData.GetTowerClears()
                .GroupBy(x => x.UserId)
                .Select(g => g.OrderByDescending(x => x.Floor).First())
                .OrderByDescending(x => x.Floor).ThenBy(x => x.Cleared)
                .Take(count)
                .Select(x => (x.UserId, gameData.GetUser(x.UserId)?.UserName ?? "?", x.Floor, x.Cleared))
                .ToList();
        }

        /// <summary>
        ///     The highest floor the website shows in full (no spoilers, Zerratar 2026-11-07): the highest floor any stream
        ///     has reached, the one after the highest cleared anywhere.
        /// </summary>
        public int Revealed()
        {
            var clears = gameData.GetTowerClears();
            return Tower.Revealed(clears.Count == 0 ? 0 : clears.Max(x => x.Floor));
        }

        /// <summary>Each floor's first clear in the world: who and when.</summary>
        public IReadOnlyDictionary<int, (string Name, DateTime Cleared)> FirstClears()
        {
            return gameData.GetTowerClears()
                .GroupBy(x => x.Floor)
                .ToDictionary(g => g.Key, g =>
                {
                    var first = g.OrderBy(x => x.Cleared).First();
                    return (gameData.GetUser(first.UserId)?.UserName ?? "?", first.Cleared);
                });
        }

        public TowerState State(Guid userId)
        {
            var tower = gameData.GetStreamTower(userId);
            var floor = tower?.Floor ?? 1;
            var definition = Tower.Get(floor);
            var progress = Tower.ParseProgress(definition, tower?.Progress);
            var first = gameData.GetTowerClears().Where(x => x.Floor == floor).OrderBy(x => x.Cleared).FirstOrDefault();
            return new TowerState
            {
                Floor = floor,
                TopFloor = Tower.TopFloor,
                Quests = definition?.Quests.Select(q => new TowerQuestProgress { Id = q.Id, Count = progress[q.Id], Amount = q.Amount }).ToArray() ?? Array.Empty<TowerQuestProgress>(),
                BossOpen = definition != null && Tower.QuestsDone(definition, progress),
                Attempts = tower?.Attempts ?? 0,
                Cleared = gameData.GetTowerClears(userId).Count,
                FirstClearBy = first != null ? gameData.GetUser(first.UserId)?.UserName : null,
            };
        }

        private StreamTower GetOrCreate(Guid userId)
        {
            var tower = gameData.GetStreamTower(userId);
            if (tower != null) return tower;
            tower = new StreamTower { Id = Guid.NewGuid(), UserId = userId, Floor = 1, Created = DateTime.UtcNow };
            gameData.Add(tower);
            return tower;
        }
    }
}
