using System;
using System.Collections.Generic;

namespace RavenNest.Combat
{
    /// <summary>
    ///     The tower (the game's docs/tower-design.md): a climb of floors that a stream makes together. Every floor has
    ///     a few stream quests (defeat its monsters) and a boss; the boss opens when the quests are done and can be
    ///     tried as often as the stream likes. Floors come in bands of ten, each band its own biome, element and town.
    ///     Shared by the server (progress, rewards), the game (the floor's enemies and boss, the quest text) and the
    ///     simulator (tuning the bosses). Floors 1 to 10 (the first band) for now; more bands with later updates.
    /// </summary>
    public static class Tower
    {
        /// <summary>Seconds players have to join a boss attempt after it is called.</summary>
        public const int BossCountdownSeconds = 60;

        /// <summary>A boss attempt ends after this many seconds if the boss still stands.</summary>
        public const int BossTimeLimitSeconds = 600;

        /// <summary>Seconds after a lost attempt before the next can start: time to regroup.</summary>
        public const int BossRetrySeconds = 120;

        /// <summary>How far players see the floor's enemies come back after they fall, in seconds.</summary>
        public const int EnemyRespawnSeconds = 20;

        /// <summary>
        ///     A boss's Health levels per level, per player of the floor's group size, before its own
        ///     <see cref="TowerBoss.HealthPool"/> share; tuned with the simulator's "tower" (docs/tower-design.md).
        /// </summary>
        public static double BossPool = 15;

        public static IReadOnlyList<TowerBand> Bands => bands;
        public static IReadOnlyList<TowerFloorDefinition> Floors => floors;

        /// <summary>The highest floor that exists so far.</summary>
        public static int TopFloor => floors.Count;

        /// <summary>The floor's definition, or null when it does not exist (yet).</summary>
        public static TowerFloorDefinition Get(int floor) => floor >= 1 && floor <= floors.Count ? floors[floor - 1] : null;

        public static TowerBand BandOf(int floor)
        {
            foreach (var band in bands) if (floor >= band.FirstFloor && floor <= band.LastFloor) return band;
            return null;
        }

        /// <summary>The combat level a floor is made for: ten per floor, so floor 100 is made for level 1,000.</summary>
        public static int Level(int floor) => Math.Max(1, floor * 10);

        // gear an enemy of a combat level has, fitted to today's island enemies (their definitions after the enemy gear
        // tuning: armour 70 + 2 per level, power 15 + 0.22, accuracy 12 + 0.18 per level)
        public static int ArmorAt(int level) => 70 + 2 * level;
        public static int PowerAt(int level) => 15 + (int)(0.22 * level);
        public static int AccuracyAt(int level) => 12 + (int)(0.18 * level);

        /// <summary>One of the floor's monsters at the floor's level: its look and style from the game, its numbers from here.</summary>
        public static EnemyDefinition Enemy(TowerFloorDefinition floor, TowerEnemy enemy)
        {
            var band = BandOf(floor.Number);
            var level = Math.Max(1, floor.Level + enemy.LevelOffset);
            var d = new EnemyDefinition
            {
                Id = "tower-" + enemy.Id,
                Name = enemy.Name,
                Area = "Tower floor " + floor.Number,
                Family = enemy.Family,
                Attack = level,
                Strength = level,
                Defense = level,
                Health = level,
                Ranged = enemy.Style == AttackStyle.Ranged ? level : 1,
                Magic = enemy.Style == AttackStyle.Magic ? level : 1,
                Power = PowerAt(level),
                Accuracy = AccuracyAt(level),
                Armor = ArmorAt(level),
                Style = enemy.Style,
                AttackInterval = enemy.AttackInterval,
            };
            if (band != null) band.Apply(d);
            return d;
        }

        /// <summary>The floor's boss: fixed numbers for its floor, so climbing is a real progression.</summary>
        public static EnemyDefinition Boss(TowerFloorDefinition floor)
        {
            var band = BandOf(floor.Number);
            var b = floor.Boss;
            var level = Math.Max(1, (int)Math.Round(floor.Level * b.LevelScale));
            var d = new EnemyDefinition
            {
                Id = "tower-boss-" + floor.Number,
                Name = b.Name,
                Area = "Tower floor " + floor.Number,
                Family = b.Family,
                Attack = level,
                Strength = level,
                Defense = level,
                // the pool: its level times the floor's group size times the boss's share (Health is only a pool here)
                Health = Math.Max(10, (int)Math.Round(level * floor.GroupSize * b.HealthPool * BossPool)),
                Ranged = b.Style == AttackStyle.Ranged ? level : 1,
                Magic = b.Style == AttackStyle.Magic ? level : 1,
                Power = (int)Math.Round(PowerAt(level) * b.PowerScale),
                Accuracy = AccuracyAt(level),
                Armor = ArmorAt(level),
                Style = b.Style,
                AttackInterval = b.AttackInterval,
                IsBoss = true,
            };
            if (band != null) band.Apply(d);
            if (b.Element != Element.Physical)
            {
                d.Element = b.Element;
                d.ElementShare = b.ElementShare;
            }
            return d;
        }

        private static readonly List<TowerBand> bands = new List<TowerBand>
        {
            new TowerBand
            {
                Number = 1, FirstFloor = 1, LastFloor = 10,
                Name = "The Overgrown Halls",
                Biome = "forest",
                Description = "Roots have broken through the old stone. Beasts and goblins nest in the green, and everything bites with poison.",
                Element = Element.Poison, ElementShare = 0.3,
                Resistance = new ElementValues { Poison = 0.5, Fire = -0.25 },
                Town = "Mossgate",
            },
        };

        private static readonly List<TowerFloorDefinition> floors = BuildFloors();

        private static List<TowerFloorDefinition> BuildFloors()
        {
            // the first band's monsters (their looks are the game's enemies with the same model id)
            var bunny = new TowerEnemy { Id = "hall-bunny", Name = "Feral Bunny", Model = "home-bunny", Family = EnemyFamily.Beast, LevelOffset = -2, AttackInterval = 1.2 };
            var goblin = new TowerEnemy { Id = "hall-goblin", Name = "Moss Goblin", Model = "ravebridge-goblin", Family = EnemyFamily.Humanoid };
            var greenMetalon = new TowerEnemy { Id = "hall-metalon-green", Name = "Vine Metalon", Model = "home-metalon-green", Family = EnemyFamily.Insect, AttackInterval = 1.2 };
            var purpleMetalon = new TowerEnemy { Id = "hall-metalon-purple", Name = "Thorn Metalon", Model = "home-metalon-purple", Family = EnemyFamily.Insect, LevelOffset = 2, AttackInterval = 1.2 };
            var redMetalon = new TowerEnemy { Id = "hall-metalon-red", Name = "Blight Metalon", Model = "home-metalon-red", Family = EnemyFamily.Insect, LevelOffset = 4, AttackInterval = 1.3 };
            var skeleton = new TowerEnemy { Id = "hall-skeleton", Name = "Rootbound Skeleton", Model = "ravebridge-skeleton", Family = EnemyFamily.Undead };
            var cow = new TowerEnemy { Id = "hall-cow", Name = "Suspicious Cow", Model = "home-cow", Family = EnemyFamily.Beast, LevelOffset = -1, AttackInterval = 1.5 };

            var list = new List<TowerFloorDefinition>
            {
                Floor(1, "The Burrows", "Something has been digging under the first hall. A lot of somethings.",
                    new[] { bunny, goblin }, Defeat(bunny, 30), Defeat(goblin, 20),
                    new TowerBoss { Name = "Nibbles the Vicious", Model = "home-bunny", Scale = 4f, Family = EnemyFamily.Beast, Line = "Run away! Run away!" }),
                Floor(2, "Goblin Market", "The goblins trade in stolen boots and very old cheese.",
                    new[] { goblin, bunny }, Defeat(goblin, 40), Defeat(bunny, 25),
                    new TowerBoss { Name = "Warchief Grubnose", Model = "ravebridge-goblin", Scale = 2.2f, Family = EnemyFamily.Humanoid, PowerScale = 1.15 }),
                Floor(3, "The Nest", "Eggs the size of barrels, and the mothers who laid them.",
                    new[] { greenMetalon, goblin }, Defeat(greenMetalon, 35), Defeat(goblin, 25),
                    new TowerBoss { Name = "The Brood Mother", Model = "home-metalon-green", Scale = 2.6f, Family = EnemyFamily.Insect, Element = Element.Poison, ElementShare = 0.5 }),
                Floor(4, "Hall of Bones", "The roots grew through the old graves, and the graves did not like it.",
                    new[] { skeleton, greenMetalon }, Defeat(skeleton, 40), Defeat(greenMetalon, 30),
                    new TowerBoss { Name = "The Rootbound King", Model = "ravebridge-skeleton", Scale = 2.3f, Family = EnemyFamily.Undead, PowerScale = 1.1 }),
                Floor(5, "Thornwood", "The trees here have thorns as long as swords, and so do the Metalons.",
                    new[] { purpleMetalon, goblin }, Defeat(purpleMetalon, 40), Defeat(goblin, 30),
                    new TowerBoss { Name = "Old Thornback", Model = "home-metalon-purple", Scale = 2.8f, Family = EnemyFamily.Insect, HealthPool = 1.1 }),
                Floor(6, "The Pasture", "A peaceful meadow with cows. The cows are watching you.",
                    new[] { cow, bunny }, Defeat(cow, 40), Defeat(bunny, 40),
                    new TowerBoss { Name = "The Moo-tant", Model = "home-cow", Scale = 3.2f, Family = EnemyFamily.Beast, Element = Element.Poison, ElementShare = 0.6, Line = "Moooo." }),
                Floor(7, "Goblin Keep", "Every goblin you met so far had a cousin. They all live here.",
                    new[] { goblin, skeleton }, Defeat(goblin, 50), Defeat(skeleton, 35),
                    new TowerBoss { Name = "King Grubnose the Second", Model = "ravebridge-goblin", Scale = 2.6f, Family = EnemyFamily.Humanoid, PowerScale = 1.2, Line = "My brother was the warchief. I am the king." }),
                Floor(8, "Blighted Grove", "The rot starts here. The red Metalons carry it.",
                    new[] { redMetalon, purpleMetalon }, Defeat(redMetalon, 40), Defeat(purpleMetalon, 40),
                    new TowerBoss { Name = "Redshell the Blighted", Model = "home-metalon-red", Scale = 3f, Family = EnemyFamily.Insect, Element = Element.Poison, ElementShare = 0.6, HealthPool = 1.1 }),
                Floor(9, "The Deep Roots", "Everything that lives in the halls comes down here to hide.",
                    new[] { redMetalon, skeleton, goblin }, Defeat(redMetalon, 45), Defeat(skeleton, 45),
                    new TowerBoss { Name = "The Gardener", Model = "ravebridge-skeleton", Scale = 2.8f, Family = EnemyFamily.Undead, Style = AttackStyle.Magic, Element = Element.Poison, ElementShare = 0.7, Line = "Everything grows. Even you, in time." }),
                Floor(10, "The Verdant Throne", "At the top of the halls, on a throne of moss, sleeps the one who made it all grow.",
                    new[] { redMetalon, purpleMetalon }, Defeat(redMetalon, 50), Defeat(purpleMetalon, 50),
                    new TowerBoss { Name = "Viridia, the Verdant Wyrm", Model = "ironhill-dragon-green", Scale = 1.6f, Family = EnemyFamily.Dragon, Element = Element.Poison, ElementShare = 0.7, HealthPool = 1.25, PowerScale = 1.2, AttackInterval = 3 }),
            };
            return list;
        }

        private static TowerFloorDefinition Floor(int number, string name, string description, TowerEnemy[] enemies, TowerQuest q1, TowerQuest q2, TowerBoss boss)
        {
            if (boss.Id == null) boss.Id = "boss-" + number;
            return new TowerFloorDefinition
            {
                Number = number,
                Name = name,
                Description = description,
                Level = Level(number),
                GroupSize = 5,
                Layout = "Tower_Overgrown",
                Enemies = enemies,
                Quests = new[] { q1, q2 },
                Boss = boss,
            };
        }

        private static TowerQuest Defeat(TowerEnemy enemy, int amount) => new TowerQuest
        {
            Id = "defeat-" + enemy.Id,
            Target = enemy.Id,
            Amount = amount,
            Text = "Defeat " + amount + " " + Plural(enemy.Name),
        };

        private static string Plural(string name)
        {
            if (name.EndsWith("y") && !name.EndsWith("ey")) return name.Substring(0, name.Length - 1) + "ies";
            if (name.EndsWith("s")) return name;
            return name + "s";
        }

        /// <summary>
        ///     Quest progress in its text form: "defeat-hall-goblin:12;defeat-hall-bunny:30". Unknown ids are kept out,
        ///     each count is capped at its quest's amount.
        /// </summary>
        public static Dictionary<string, int> ParseProgress(TowerFloorDefinition floor, string text)
        {
            var result = new Dictionary<string, int>();
            if (floor == null) return result;
            foreach (var q in floor.Quests) result[q.Id] = 0;
            if (string.IsNullOrWhiteSpace(text)) return result;
            foreach (var part in text.Split(';'))
            {
                var colon = part.IndexOf(':');
                if (colon <= 0) continue;
                var id = part.Substring(0, colon).Trim();
                if (!result.ContainsKey(id)) continue;
                if (!int.TryParse(part.Substring(colon + 1), out var count)) continue;
                var quest = Array.Find(floor.Quests, q => q.Id == id);
                result[id] = Math.Max(0, Math.Min(quest.Amount, count));
            }
            return result;
        }

        public static string FormatProgress(Dictionary<string, int> progress)
        {
            var parts = new List<string>();
            foreach (var kv in progress) parts.Add(kv.Key + ":" + kv.Value);
            return string.Join(";", parts);
        }

        public static bool QuestsDone(TowerFloorDefinition floor, Dictionary<string, int> progress)
        {
            if (floor == null) return false;
            foreach (var q in floor.Quests)
                if (!progress.TryGetValue(q.Id, out var count) || count < q.Amount) return false;
            return true;
        }
    }

    /// <summary>Ten floors with one biome, element and town (the town opens the band).</summary>
    public sealed class TowerBand
    {
        public int Number;
        public int FirstFloor;
        public int LastFloor;
        public string Name = "";
        /// <summary>The biome's kit in the game: "forest", later "ice", "forge", "storm", "crypt".</summary>
        public string Biome = "";
        public string Description = "";
        /// <summary>The element the band's monsters fight with, and their share of each hit.</summary>
        public Element Element;
        public double ElementShare;
        /// <summary>What the band's monsters resist (positive) and are weak to (negative).</summary>
        public ElementValues Resistance;
        /// <summary>The town that opens the band (a calm floor, later).</summary>
        public string Town = "";

        public void Apply(EnemyDefinition d)
        {
            d.Element = Element;
            d.ElementShare = ElementShare;
            d.Resistance = Resistance;
        }
    }

    public sealed class TowerFloorDefinition
    {
        public int Number;
        public string Name = "";
        public string Description = "";
        /// <summary>The combat level the floor is made for.</summary>
        public int Level;
        /// <summary>How many players of <see cref="Level"/> the boss is made for.</summary>
        public int GroupSize;
        /// <summary>The floor's scene in the game; floors of a band share a few layouts.</summary>
        public string Layout = "";
        public TowerEnemy[] Enemies = Array.Empty<TowerEnemy>();
        public TowerQuest[] Quests = Array.Empty<TowerQuest>();
        public TowerBoss Boss;
    }

    /// <summary>A kind of monster on a floor.</summary>
    public sealed class TowerEnemy
    {
        public string Id = "";
        public string Name = "";
        /// <summary>The game enemy whose look it takes (an enemy definition id, such as "home-bunny").</summary>
        public string Model = "";
        public EnemyFamily Family;
        public AttackStyle Style = AttackStyle.Melee;
        /// <summary>Levels above (or below) the floor's level.</summary>
        public int LevelOffset;
        public double AttackInterval = 1;
    }

    /// <summary>A floor quest for the whole stream: defeat a number of one kind of the floor's monsters.</summary>
    public sealed class TowerQuest
    {
        public string Id = "";
        public string Text = "";
        /// <summary>The <see cref="TowerEnemy.Id"/> to defeat.</summary>
        public string Target = "";
        public int Amount;
    }

    public sealed class TowerBoss
    {
        public string Id;
        public string Name = "";
        /// <summary>The game enemy whose look it takes, made <see cref="Scale"/> times bigger.</summary>
        public string Model = "";
        public float Scale = 2.5f;
        public EnemyFamily Family;
        public AttackStyle Style = AttackStyle.Melee;
        /// <summary>Its level as a share of the floor's.</summary>
        public double LevelScale = 1.0;
        /// <summary>Its share of the standard boss pool (<see cref="Tower.BossPool"/>): 1.25 for a tougher one.</summary>
        public double HealthPool = 1;
        /// <summary>Its power as a share of an enemy of its level.</summary>
        public double PowerScale = 1;
        public double AttackInterval = 2.5;
        /// <summary>Its own element, instead of the band's, when not Physical.</summary>
        public Element Element;
        public double ElementShare;
        /// <summary>Every this many swings it slams the ground and hits everyone near it (0: never).</summary>
        public int SlamEvery = 5;
        /// <summary>A slam's damage as a share of a normal hit, against each player it reaches.</summary>
        public double SlamShare = 0.6;
        /// <summary>What it says when the attempt starts, for chat; empty for nothing.</summary>
        public string Line = "";
    }
}
