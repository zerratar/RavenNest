using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RavenNest.BusinessLogic.Data;
using RavenNest.Combat;
using RavenNest.DataModels;
using RavenNest.Models;
using Skill = RavenNest.Models.Skill;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    ///     The tower on the server (the game's docs/tower-design.md and docs/tower-worlds-proposal.md section 12): which
    ///     part each stream is on, the progress of the part's objectives, boss attempts, clears and their rewards.
    ///     Progress belongs to the stream (the streamer's user), like the village: everyone playing there climbs
    ///     together, and a part once cleared stays cleared. Players see floors with parts; here everything is a step
    ///     (a part's number through every floor: StreamTower.Floor, TowerClear.Floor), and RavenNest.Combat.Tower maps
    ///     between the two. The parts are RavenNest.Combat.Tower's, shared with the game, which reports what happens on
    ///     them; gathering is counted here from what the server sees the stream's players gather anyway.
    /// </summary>
    public class TowerManager
    {
        /// <summary>Kills one report may add to an objective: a game reports every few seconds, so more is a mistake.</summary>
        private const int MaxKillsPerReport = 500;

        /// <summary>Coins per step for each player in a won attempt; twice that for the world's first clear of the part.</summary>
        private const long CoinsPerFloor = 250;

        /// <summary>Village coins per step when a part is cleared.</summary>
        private const long VillageCoinsPerFloor = 1000;

        /// <summary>
        ///     Seconds between two finds of one objective at least: the game finds a hidden thing after minutes of
        ///     searching (Tower.FindSeconds), so finds any faster than this are not from a game playing by the rules.
        /// </summary>
        private const int MinFindSeconds = 45;

        /// <summary>Gathering rounds kept in memory before they are written to a stream's progress.</summary>
        private const int GatherFlush = 20;

        /// <summary>
        ///     Minutes a character counts as gathering for a part's objective after its last round for it: how many
        ///     of the stream's players gather sets what a round is worth (TowerPace.GatherWeight). A round comes about
        ///     every ten seconds, so this bridges a walk to the next spot or a full bag, not a change of task.
        /// </summary>
        private const double GathererMinutes = 2;

        /// <summary>The most heads a game's report is taken to have had on the part (a mistake beyond it).</summary>
        private const double MaxHeads = 500;

        /// <summary>The last hit on a floor boss: the achievement its title comes from (who gets it: Tower.LastHits).</summary>
        public const string LastHitAchievement = "finisher";

        private readonly GameData gameData;
        private readonly GameManager gameManager;
        private readonly object mutex = new object();
        private readonly ConcurrentDictionary<Guid, DateTime> lastClear = new ConcurrentDictionary<Guid, DateTime>();
        private readonly Dictionary<(Guid Tower, string Objective), DateTime> lastFind = new Dictionary<(Guid, string), DateTime>();
        // gathering since the last write, per stream and skill (by the skill's name, as the objectives name them): the
        // rounds, and what they are worth together (each by how many of the stream's players were gathering then)
        private readonly Dictionary<Guid, Dictionary<string, (int Rounds, double Worth)>> gathered = new Dictionary<Guid, Dictionary<string, (int, double)>>();
        // who of a stream's players gathers for its part's objectives, and when they last did
        private readonly Dictionary<Guid, Dictionary<Guid, DateTime>> gatherers = new Dictionary<Guid, Dictionary<Guid, DateTime>>();
        private static readonly string[] SkillNames = Enum.GetNames(typeof(Skill));

        public TowerManager(GameData gameData, GameManager gameManager)
        {
            this.gameData = gameData;
            this.gameManager = gameManager;
            QuestTracker.Tower = this;
        }

        public TowerState GetState(SessionToken token)
        {
            var session = gameData.GetSession(token.SessionId);
            return session != null ? State(session.UserId) : null;
        }

        /// <summary>
        ///     What happened on the stream's part since the game's last report, as counts ("defeat-hall-goblin:3;kills:5;
        ///     find-boots:1;heads:500"): kills per kill objective, every kill (gather objectives count them too), hidden
        ///     things found, and the heads that were on the part as the kills fell. Each kill is worth more with few
        ///     heads on the part and less with many (TowerPace.KillWeight; 1 when the game does not say); what is not a
        ///     whole kill yet is kept with the progress. A report from any other part (an old part, a stale report)
        ///     counts for nothing.
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

                var report = Tower.ParseCounts(counts);
                // hidden things are found minutes apart: a find too soon after the last waits for a later report
                var now = DateTime.UtcNow;
                foreach (var q in definition.Objectives)
                {
                    if (q.Kind != TowerObjectiveKind.Find || !report.TryGetValue(q.Id, out var n) || n <= 0) continue;
                    if (lastFind.TryGetValue((tower.Id, q.Id), out var last) && (now - last).TotalSeconds < MinFindSeconds) report.Remove(q.Id);
                }

                var progress = Tower.ParseProgress(definition, tower.Progress);
                var fractions = Tower.ParseFractions(tower.Progress);
                var before = new Dictionary<string, int>(progress);
                var changed = TakeGathered(tower, definition, progress, fractions);
                // what a kill is worth: by the heads the game counted on the part as the kills fell
                var weight = report.TryGetValue(Tower.Heads, out var heads) && heads > 0
                    ? TowerPace.KillWeight(definition.Floor, Math.Min(MaxHeads, heads / 100.0)) : 1;
                changed |= Tower.AddReport(definition, progress, report, MaxKillsPerReport, fractions, weight);
                if (changed)
                {
                    foreach (var q in definition.Objectives)
                        if (q.Kind == TowerObjectiveKind.Find && progress[q.Id] > before[q.Id]) lastFind[(tower.Id, q.Id)] = now;
                    tower.Progress = Tower.FormatProgress(progress, fractions);
                    tower.Updated = now;
                    gameData.Touch();
                }
                return State(session.UserId);
            }
        }

        /// <summary>
        ///     A gathering round by a character (QuestTracker.Gathered, on whatever thread the task processors run): when
        ///     the part of the stream it plays on has a gather objective for the skill, it counts: one with two of the
        ///     stream's players gathering for the part, more for a lone gatherer, less for a crowd
        ///     (TowerPace.GatherWeight, by who gathered for it in the last <see cref="GathererMinutes"/> minutes; fixed as
        ///     the round is made, so nothing is gained by stopping or starting). Counted, never taken: the character
        ///     keeps what it gathered. Kept in memory and written every few rounds, and whenever the stream's progress is
        ///     read or changed.
        /// </summary>
        internal void Gathered(Character character, Skill skill)
        {
            var stream = character?.UserIdLock;
            if (stream == null) return;
            var tower = gameData.GetStreamTower(stream.Value);
            if (tower == null) return;
            var definition = Tower.Get(tower.Floor);
            var index = (int)skill;
            if (definition == null || index < 0 || index >= SkillNames.Length) return;
            var name = SkillNames[index];
            if (!Tower.Gathers(definition, name)) return;
            lock (mutex)
            {
                // who gathers for the part now: this character, and whoever did in the last minutes
                var now = DateTime.UtcNow;
                if (!gatherers.TryGetValue(tower.Id, out var who)) gatherers[tower.Id] = who = new Dictionary<Guid, DateTime>();
                who[character.Id] = now;
                var active = 0;
                List<Guid> gone = null;
                foreach (var kv in who)
                {
                    if ((now - kv.Value).TotalMinutes <= GathererMinutes) active++;
                    else (gone ??= new List<Guid>()).Add(kv.Key);
                }
                if (gone != null) foreach (var id in gone) who.Remove(id);

                if (!gathered.TryGetValue(tower.Id, out var counts)) gathered[tower.Id] = counts = new Dictionary<string, (int, double)>();
                counts.TryGetValue(name, out var n);
                counts[name] = (n.Rounds + 1, n.Worth + TowerPace.GatherWeight(active));
                if (n.Rounds + 1 < GatherFlush) return;
                var progress = Tower.ParseProgress(definition, tower.Progress);
                var fractions = Tower.ParseFractions(tower.Progress);
                if (!TakeGathered(tower, definition, progress, fractions)) return;
                tower.Progress = Tower.FormatProgress(progress, fractions);
                tower.Updated = now;
                gameData.Touch();
            }
        }

        // the rounds kept in memory go into the progress (the caller holds the lock and saves when this is true)
        private bool TakeGathered(StreamTower tower, TowerFloorDefinition definition, Dictionary<string, int> progress, Dictionary<string, double> fractions)
        {
            if (!gathered.TryGetValue(tower.Id, out var counts) || counts.Count == 0) return false;
            var changed = false;
            foreach (var kv in counts) changed |= Tower.AddGathered(definition, progress, kv.Key, kv.Value.Worth, fractions);
            counts.Clear();
            return changed;
        }

        /// <summary>
        ///     Kills per character and kind on any part of the tower, for the characters' own quests
        ///     (QuestTracker.TowerKills): characters of this session only, each count capped like a part's report.
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

            // knocked out by a guardian: only the stream's own part counts (Nibbles is the first part's, for the hidden "nibbled")
            var floor = GetOrCreate(session.UserId).Floor;
            foreach (var f in (report.Falls ?? Array.Empty<TowerCharacterFall>()).Take(200))
            {
                if (f == null || f.Floor != floor) continue;
                var character = characters.FirstOrDefault(c => c.Id == f.CharacterId);
                if (character != null) QuestTracker.TowerFell(character, f.Floor);
            }
        }

        /// <summary>A boss attempt starts on the stream's part: counted, if the part's objectives are done.</summary>
        public TowerState BossStarted(SessionToken token, int floor)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null) return null;
            lock (mutex)
            {
                var tower = GetOrCreate(session.UserId);
                var definition = Tower.Get(tower.Floor);
                if (definition != null && floor == tower.Floor && Tower.ObjectivesDone(definition, Progress(tower, definition)))
                {
                    tower.Attempts++;
                    tower.Updated = DateTime.UtcNow;
                    gameData.Touch();
                }
                return State(session.UserId);
            }
        }

        /// <summary>
        ///     The stream beat its part's guardian: the part is cleared, the next one opens, and everyone who took part
        ///     (in the session) gets coins and a chest. A mini boss pays the usual; a floor boss (the last part of a
        ///     floor) is the world first that is announced, opens the next floor and gives whoever landed the last blow
        ///     their title. Refused when it is not the stream's part or its objectives are not done.
        /// </summary>
        public TowerClearResult BossWon(SessionToken token, int floor, TowerClearReport report)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null) return new TowerClearResult { Message = "No game session." };
            if (lastClear.TryGetValue(session.Id, out var last) && DateTime.UtcNow - last < TimeSpan.FromSeconds(30))
                return new TowerClearResult { Message = "A part was cleared moments ago.", State = State(session.UserId) };

            var characterIds = (report?.Characters ?? Array.Empty<Guid>()).Distinct().ToArray();
            TowerClear clear;
            bool partFirst;
            bool streamFirst;
            bool floorBoss;
            double pressure;
            var sessionCharacters = gameData.GetActiveSessionCharacters(session);
            // only characters of this session count, for the rewards and for the last blow
            var ids = characterIds.Where(id => sessionCharacters.Any(c => c.Id == id)).ToArray();
            Guid[] lastHits;
            lock (mutex)
            {
                var tower = GetOrCreate(session.UserId);
                var definition = Tower.Get(tower.Floor);
                if (definition == null || floor != tower.Floor)
                    return new TowerClearResult { Message = "That is not the part the stream is on.", State = State(session.UserId) };
                if (!Tower.ObjectivesDone(definition, Progress(tower, definition)))
                    return new TowerClearResult { Message = "The part's objectives are not done.", State = State(session.UserId) };

                floorBoss = definition.IsFloorBoss;
                partFirst = !gameData.GetTowerClears().Any(x => x.Floor == floor);
                // the stream's own first clear of the part. Today always: a stream clears a part once, then it is on
                // the next. Asked anyway, so class gear's chance stays right if a part can ever be cleared again.
                streamFirst = !gameData.GetTowerClears(session.UserId).Any(x => x.Floor == floor);
                var strength = report != null && report.Strength > 0 ? CombatMath.Clamp(report.Strength, TowerPressure.MinStrength, TowerPressure.MaxStrength) : TowerPressure.MinStrength;
                pressure = TowerPressure.Of(ids.Length, strength, Tower.BossGroup(definition));
                lastHits = floorBoss ? Tower.LastHits(report?.LastHits, ids, Tower.MaxLastHits) : Array.Empty<Guid>();
                clear = new TowerClear
                {
                    Id = Guid.NewGuid(),
                    UserId = session.UserId,
                    Floor = floor,
                    Cleared = DateTime.UtcNow,
                    Players = ids.Length,
                    Attempts = Math.Max(1, tower.Attempts),
                    Strength = strength,
                    LastHitCharacterId = lastHits.Length > 0 ? lastHits[0] : (Guid?)null,
                };
                gameData.Add(clear);

                tower.Floor = floor + 1;
                tower.Progress = null;
                tower.Attempts = 0;
                tower.Updated = DateTime.UtcNow;
                gathered.Remove(tower.Id);
                gatherers.Remove(tower.Id);
                lastClear[session.Id] = DateTime.UtcNow;
            }

            // the rewards: coins for each player, a chest from the dungeon table by the step (step 10's tier is epic),
            // two items from a floor boss and from every fifth part; the village gets its share
            var coins = CoinsPerFloor * floor * (partFirst ? 2 : 1);
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

            // the characters' own quests count the clear (the towns' chains), the climber keeps the highest step
            foreach (var id in ids)
            {
                var character = sessionCharacters.FirstOrDefault(c => c.Id == id);
                if (character != null) QuestTracker.TowerCleared(character, floor);
            }

            // the last blow on a floor boss: a title, nothing else (cosmetic, so nobody fights over it)
            foreach (var id in lastHits)
            {
                var character = sessionCharacters.FirstOrDefault(c => c.Id == id);
                if (character != null) QuestTracker.TowerLastHit(character);
            }

            var tier = Math.Min(4, (floor - 1) / 3);
            var rewards = gameManager.RollTowerChest(session, tier, ids, Tower.ChestItems(floor));
            // and class gear made for the part, with a gem of the floor's element in it (RavenNest.Combat.TowerLoot):
            // everyone who fought on the stream's first clear of the part
            var gear = gameManager.RollTowerGear(session, floor, ids, streamFirst);
            if (gear.Length > 0) rewards = rewards.Concat(gear).ToArray();
            // and the guardian's own trophies, if it has any (the Gardener's Dark Fortress pieces)
            var trophies = gameManager.RollTowerTrophies(session, floor, ids, streamFirst);
            if (trophies.Length > 0) rewards = rewards.Concat(trophies).ToArray();
            // and the Legendaries (docs/legendary-design.md): worn ones awaken, a guardian on the top floors may give one
            var legends = gameManager.TowerBossFell(session, floor, ids);
            if (legends.Length > 0) rewards = rewards.Concat(legends).ToArray();
            gameData.Touch();

            return new TowerClearResult
            {
                Success = true,
                Floor = floor,
                FloorBoss = floorBoss,
                WorldFirst = floorBoss && partFirst,
                PartFirst = partFirst,
                Coins = coins,
                Rewards = rewards,
                LastHitBy = lastHits,
                Pressure = pressure,
                State = State(session.UserId),
            };
        }

        /// <summary>A stream's place on the leaderboard: the highest part it has cleared (as a step, and as its floor and part).</summary>
        public sealed class Leader
        {
            public Guid UserId;
            public string Name;
            /// <summary>The highest step cleared.</summary>
            public int Step;
            public int Floor;
            public int Part;
            /// <summary>Floors whose boss the stream has beaten.</summary>
            public int FloorsCleared;
            public DateTime Cleared;
        }

        /// <summary>
        ///     The highest part each stream has cleared, for the website: by floor, then by part, earlier first on a tie
        ///     (which is the order of the steps).
        /// </summary>
        public IReadOnlyList<Leader> Leaderboard(int count)
        {
            return gameData.GetTowerClears()
                .Where(x => Tower.Get(x.Floor) != null)
                .GroupBy(x => x.UserId)
                .Select(g => g.OrderByDescending(x => x.Floor).First())
                .OrderByDescending(x => Tower.FloorOf(x.Floor)).ThenByDescending(x => Tower.PartOf(x.Floor)).ThenBy(x => x.Cleared)
                .Take(count)
                .Select(x => new Leader
                {
                    UserId = x.UserId,
                    Name = gameData.GetUser(x.UserId)?.UserName ?? "?",
                    Step = x.Floor,
                    Floor = Tower.FloorOf(x.Floor),
                    Part = Tower.PartOf(x.Floor),
                    FloorsCleared = Tower.FloorsCleared(x.Floor),
                    Cleared = x.Cleared,
                })
                .ToList();
        }

        /// <summary>
        ///     The highest step the website shows in full (no spoilers, Zerratar 2026-11-07): the highest part any stream
        ///     has reached, the one after the highest cleared anywhere.
        /// </summary>
        public int Revealed()
        {
            var clears = gameData.GetTowerClears();
            return Tower.Revealed(clears.Count == 0 ? 0 : clears.Max(x => x.Floor));
        }

        /// <summary>A part's first clear in the world.</summary>
        public sealed class FirstClear
        {
            public string Name;
            public DateTime Cleared;
            public int Players;
            public int Attempts;
            /// <summary>Who landed the last blow (a floor boss's clear), or null.</summary>
            public string LastHitBy;
        }

        /// <summary>
        ///     Each part's first clear in the world, by step: who, when, with how many. A floor boss's is the world first
        ///     that was announced; the others are the quieter record for the website.
        /// </summary>
        public IReadOnlyDictionary<int, FirstClear> FirstClears()
        {
            return gameData.GetTowerClears()
                .GroupBy(x => x.Floor)
                .ToDictionary(g => g.Key, g =>
                {
                    var first = g.OrderBy(x => x.Cleared).First();
                    return new FirstClear
                    {
                        Name = gameData.GetUser(first.UserId)?.UserName ?? "?",
                        Cleared = first.Cleared,
                        Players = first.Players,
                        Attempts = first.Attempts,
                        LastHitBy = first.LastHitCharacterId != null ? gameData.GetCharacter(first.LastHitCharacterId.Value)?.Name : null,
                    };
                });
        }

        /// <summary>The stream's step, or 0 when it has never been in the tower (for the website's town page).</summary>
        public int StepOf(Guid userId) => gameData.GetStreamTower(userId)?.Floor ?? 0;

        public TowerState State(Guid userId)
        {
            var tower = gameData.GetStreamTower(userId);
            var floor = tower?.Floor ?? 1;
            var definition = Tower.Get(floor);
            Dictionary<string, int> progress;
            lock (mutex) progress = tower != null ? Progress(tower, definition) : Tower.ParseProgress(definition, null);
            var first = gameData.GetTowerClears().Where(x => x.Floor == floor).OrderBy(x => x.Cleared).FirstOrDefault();
            return new TowerState
            {
                Floor = floor,
                TopFloor = Tower.TopFloor,
                Objectives = definition?.Objectives.Where(q => q.Counted)
                    .Select(q => new TowerObjectiveProgress { Id = q.Id, Kind = (int)q.Kind, Count = progress[q.Id], Amount = q.Amount }).ToArray() ?? Array.Empty<TowerObjectiveProgress>(),
                BossOpen = definition != null && Tower.ObjectivesDone(definition, progress),
                Attempts = tower?.Attempts ?? 0,
                Cleared = gameData.GetTowerClears(userId).Count,
                FirstClearBy = first != null ? gameData.GetUser(first.UserId)?.UserName : null,
            };
        }

        // the part's progress with the gathering kept in memory counted in (and written when it changed anything);
        // the caller holds the lock
        private Dictionary<string, int> Progress(StreamTower tower, TowerFloorDefinition definition)
        {
            var progress = Tower.ParseProgress(definition, tower.Progress);
            var fractions = Tower.ParseFractions(tower.Progress);
            if (definition != null && TakeGathered(tower, definition, progress, fractions))
            {
                tower.Progress = Tower.FormatProgress(progress, fractions);
                tower.Updated = DateTime.UtcNow;
                gameData.Touch();
            }
            return progress;
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
