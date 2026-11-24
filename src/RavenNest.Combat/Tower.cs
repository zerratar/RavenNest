using System;
using System.Collections.Generic;

namespace RavenNest.Combat
{
    /// <summary>
    ///     The tower (the game's docs/tower-design.md and docs/tower-worlds-proposal.md section 12): a climb that a
    ///     stream makes together. What players see: the tower has floors, each floor a world with its own town, biome
    ///     and element, and each floor has parts. Every part has objectives (defeat its monsters, gather, find hidden
    ///     things) and a guardian: a mini boss, or on the floor's last part the floor boss, which opens the next floor.
    ///     The names in this code are older than those words: a <see cref="TowerBand"/> is a floor, a
    ///     <see cref="TowerFloorDefinition"/> is one part, and its <see cref="TowerFloorDefinition.Number"/> is the
    ///     part's step, counted from 1 through every floor (what the server stores: StreamTower.Floor, TowerClear.Floor).
    ///     The mapping between steps and floors with parts is here (<see cref="FloorOf"/>, <see cref="PartOf"/>,
    ///     <see cref="StepOf"/>, <see cref="Label"/>), and nothing may assume a number of floors or parts in a floor.
    ///     Shared by the server (progress, rewards), the game (the part's enemies and boss, the objectives' text) and
    ///     the simulator (tuning the bosses and the scaling). Three floors of ten parts so far; more keep coming.
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
        public static IReadOnlyList<TowerTownDefinition> Towns => towns;

        public static TowerTownDefinition Town(string key)
        {
            foreach (var t in towns) if (string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        /// <summary>The town a stream can visit: the one of the floor its current step is on.</summary>
        public static TowerTownDefinition TownFor(int floor)
        {
            var band = BandOf(Math.Max(1, Math.Min(floor, TopFloor)));
            foreach (var t in towns) if (band != null && t.Band == band.Number) return t;
            return towns.Count > 0 ? towns[0] : null;
        }

        /// <summary>
        ///     The highest floor the website may show (Zerratar 2026-11-07: no spoilers): the highest any stream has
        ///     reached, the floor after the highest cleared one. <paramref name="highestCleared"/> is the world's.
        /// </summary>
        public static int Revealed(int highestCleared) => Math.Max(1, Math.Min(TopFloor, highestCleared + 1));

        private static readonly List<TowerTownDefinition> towns = new List<TowerTownDefinition>
        {
            new TowerTownDefinition
            {
                Key = "mossgate", Name = "Mossgate", Band = 1, Layout = "Tower_Mossgate",
                Description = "A mossy hamlet at the tower's foot, where climbers rest, trade rumors and lose their bunnies.",
                People = new[]
                {
                    new TowerNpc { Key = "elm", Name = "Warden Elm", Role = "Keeper of the tower gate", Chain = "warden" },
                    new TowerNpc { Key = "bramble", Name = "Bramble", Role = "Herbalist", Chain = "bramble" },
                    new TowerNpc { Key = "pip", Name = "Pip", Role = "A boy who lost his bunny", Chain = "nibbles" },
                    new TowerNpc { Key = "mossbeard", Name = "Old Mossbeard", Role = "Innkeeper of the Sleepy Stump" },
                },
            },
            new TowerTownDefinition
            {
                Key = "rimeholt", Name = "Rimeholt", Band = 2, Layout = "Tower_Rimeholt",
                Description = "A snowed-in hamlet on a ledge of the tower, kept alive by one forge, one mead hall and a lot of stubbornness.",
                People = new[]
                {
                    new TowerNpc { Key = "hilda", Name = "Hilda Embersmith", Role = "The smith who keeps the forge lit", Chain = "embers" },
                    new TowerNpc { Key = "tove", Name = "Old Tove", Role = "An ice fisher with a frozen beard", Chain = "tove" },
                    new TowerNpc { Key = "ylva", Name = "Ylva", Role = "A girl whose duck flew up the tower", Chain = "duck" },
                    new TowerNpc { Key = "gunnhild", Name = "Big Gunnhild", Role = "Keeper of the Thawing Mead hall" },
                },
            },
            new TowerTownDefinition
            {
                Key = "galewharf", Name = "Galewharf", Band = 3, Layout = "Tower_Galewharf",
                Description = "A harbor on the clouds where sky sailors moor their ships, mend their sails and swear at the weather.",
                People = new[]
                {
                    new TowerNpc { Key = "brisa", Name = "Captain Brisa Windlass", Role = "Harbormaster of the cloud docks", Chain = "windlass" },
                    new TowerNpc { Key = "volt", Name = "Tinker Volt", Role = "An inventor who bottles lightning", Chain = "bottled" },
                    new TowerNpc { Key = "tessa", Name = "Tessa", Role = "A shepherd girl whose sheep floated away", Chain = "woolly" },
                    new TowerNpc { Key = "hob", Name = "Skipper Hob", Role = "Keeper of the Crow's Nest inn" },
                },
            },
        };
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

        // ---- Floors and parts: what players see (docs/tower-worlds-proposal.md section 12) -------------------------
        // A step is a part's number counted through the whole tower (1, 2, 3...); the server keeps steps. A floor is a
        // band; its parts are the band's steps. Every caller asks here, so floors may have any number of parts.

        /// <summary>How many floors exist so far.</summary>
        public static int FloorCount => bands.Count;

        /// <summary>The floor (a world with its town) by its number, or null.</summary>
        public static TowerBand FloorInfo(int floor)
        {
            foreach (var band in bands) if (band.Number == floor) return band;
            return null;
        }

        /// <summary>The floor a step is on, 0 when the step does not exist.</summary>
        public static int FloorOf(int step) => BandOf(step)?.Number ?? 0;

        /// <summary>The part of its floor a step is (1 is the floor's first), 0 when the step does not exist.</summary>
        public static int PartOf(int step)
        {
            var band = BandOf(step);
            return band != null ? step - band.FirstFloor + 1 : 0;
        }

        /// <summary>How many parts a floor has, 0 when the floor does not exist.</summary>
        public static int PartsIn(int floor) => FloorInfo(floor)?.Parts ?? 0;

        /// <summary>The step of a floor's part, 0 when there is no such part.</summary>
        public static int StepOf(int floor, int part)
        {
            var band = FloorInfo(floor);
            return band != null && part >= 1 && part <= band.Parts ? band.FirstFloor + part - 1 : 0;
        }

        /// <summary>The step of a floor's first and last part (the last is the floor boss's), 0 when the floor does not exist.</summary>
        public static int FirstStep(int floor) => FloorInfo(floor)?.FirstFloor ?? 0;
        public static int LastStep(int floor) => FloorInfo(floor)?.LastFloor ?? 0;

        /// <summary>The guardian of this step is its floor's boss (the last part); every other part's is a mini boss.</summary>
        public static bool IsFloorBoss(int step)
        {
            var band = BandOf(step);
            return band != null && step == band.LastFloor;
        }

        /// <summary>
        ///     The latest part of a floor a stream on <paramref name="streamStep"/> has reached, as a step: the stream's
        ///     own part on its own floor, the last part of a floor below it, 0 for a floor above it.
        /// </summary>
        public static int LatestStep(int floor, int streamStep)
        {
            var band = FloorInfo(floor);
            if (band == null || streamStep < band.FirstFloor) return 0;
            return Math.Min(streamStep, band.LastFloor);
        }

        /// <summary>Floors whose boss has fallen once every step up to <paramref name="clearedStep"/> is cleared.</summary>
        public static int FloorsCleared(int clearedStep)
        {
            var count = 0;
            foreach (var band in bands) if (clearedStep >= band.LastFloor) count++;
            return count;
        }

        /// <summary>"Floor 2, part 4": a step in the players' words (for a step past the top: the top's).</summary>
        public static string Label(int step)
        {
            var band = BandOf(Math.Max(1, Math.Min(step, TopFloor)));
            if (band == null) return "";
            var s = Math.Max(band.FirstFloor, Math.Min(step, band.LastFloor));
            return "Floor " + band.Number + ", part " + (s - band.FirstFloor + 1);
        }

        /// <summary>"2-4": a step the short way, as chat takes it (!tower 2-4).</summary>
        public static string Short(int step)
        {
            var band = BandOf(step);
            return band != null ? band.Number + "-" + (step - band.FirstFloor + 1) : "";
        }

        /// <summary>
        ///     What a player typed after !tower as a place: "2-4", "2 4", "floor 2 part 4", "floor 2" (part 0: the floor
        ///     as a whole), "part 4" or "4" alone (floor 0: the floor the group is on). False when it is none of these.
        /// </summary>
        public static bool TryParsePlace(string text, out int floor, out int part)
        {
            floor = 0; part = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var words = text.Trim().ToLowerInvariant().Split(new[] { ' ', '-', ',', '.', ':', '/' }, StringSplitOptions.RemoveEmptyEntries);
            var numbers = new List<int>();
            var sawFloor = false; var sawPart = false; var partFirst = false;
            foreach (var w in words)
            {
                if (w == "floor" || w == "f") { sawFloor = true; continue; }
                if (w == "part" || w == "p") { if (!sawFloor && numbers.Count == 0) partFirst = true; sawPart = true; continue; }
                if (!int.TryParse(w, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n)) return false;
                numbers.Add(n);
            }
            if (numbers.Count == 0 || numbers.Count > 2) return false;
            if (numbers.Count == 2)
            {
                // "part 4 floor 2" is the only order with the part first
                if (partFirst && sawFloor) { part = numbers[0]; floor = numbers[1]; }
                else { floor = numbers[0]; part = numbers[1]; }
                return floor > 0 && part > 0;
            }
            if (sawFloor && !sawPart) floor = numbers[0];
            else part = numbers[0];
            return numbers[0] > 0;
        }

        /// <summary>
        ///     Level sync (Zerratar 2026-11-08): on a floor nobody fights above the floor's level plus this share, so a
        ///     fixed boss stays a climb for a stream of any size and level. Gear, skill trees and gems still count.
        /// </summary>
        public static double SyncMargin = 1.1;

        /// <summary>The highest level a skill counts at on a floor.</summary>
        public static int SyncLevel(int floor) => (int)Math.Ceiling(Level(floor) * SyncMargin);

        // the best gear wearable at a level (the simulator's gearcaps, from the item list, 2026-11-08): level, melee
        // power, melee accuracy, ranged power, ranged accuracy, magic power, magic accuracy, armour
        private static readonly int[,] GearSteps =
        {
            { 1, 118, 113, 92, 77, 92, 77, 100 },
            { 10, 118, 113, 100, 85, 100, 85, 134 },
            { 20, 118, 113, 100, 91, 100, 91, 166 },
            { 30, 118, 113, 109, 94, 109, 94, 179 },
            { 40, 144, 139, 109, 94, 109, 94, 179 },
            { 50, 144, 139, 122, 107, 122, 107, 239 },
            { 60, 144, 139, 128, 123, 122, 107, 239 },
            { 70, 150, 129, 148, 133, 148, 133, 328 },
            { 80, 163, 158, 148, 133, 148, 133, 328 },
            { 90, 163, 158, 162, 147, 162, 147, 392 },
            { 120, 185, 180, 180, 165, 180, 165, 572 },
            { 150, 211, 186, 206, 191, 206, 191, 653 },
            { 200, 246, 216, 238, 223, 238, 223, 743 },
            { 300, 289, 253, 278, 263, 278, 263, 843 },
            { 500, 587, 479, 546, 531, 546, 531, 1600 },
            { 999, 927, 750, 854, 839, 854, 839, 2631 },
        };

        /// <summary>
        ///     Level sync for gear: on a floor, power, accuracy and armour count up to the best gear wearable at the floor's
        ///     sync level, so gear above it gives nothing more there; gear below it counts as it is, and every other gear stat
        ///     (crits, elements, attack speed, enchantments) counts in full.
        /// </summary>
        public static void CapGear(CombatSheet sheet, int level)
        {
            if (sheet == null || level <= 0) return;
            var row = 0;
            for (var i = 0; i < GearSteps.GetLength(0); ++i) if (GearSteps[i, 0] <= level) row = i;
            sheet.Melee.Power = Math.Min(sheet.Melee.Power, GearSteps[row, 1]);
            sheet.Melee.Accuracy = Math.Min(sheet.Melee.Accuracy, GearSteps[row, 2]);
            sheet.Ranged.Power = Math.Min(sheet.Ranged.Power, GearSteps[row, 3]);
            sheet.Ranged.Accuracy = Math.Min(sheet.Ranged.Accuracy, GearSteps[row, 4]);
            sheet.Magic.Power = Math.Min(sheet.Magic.Power, GearSteps[row, 5]);
            sheet.Magic.Accuracy = Math.Min(sheet.Magic.Accuracy, GearSteps[row, 6]);
            sheet.Healing.Power = Math.Min(sheet.Healing.Power, GearSteps[row, 5]);
            sheet.Armor = Math.Min(sheet.Armor, GearSteps[row, 7]);
        }

        /// <summary>The most armour gear counts for at a sync level (the best-gear table).</summary>
        public static int MaxArmor(int level)
        {
            var row = 0;
            for (var i = 0; i < GearSteps.GetLength(0); ++i) if (GearSteps[i, 0] <= level) row = i;
            return GearSteps[row, 7];
        }

        /// <summary>
        ///     The health gear gives on a synced floor: armour's share is capped with the armour (a level 999 set's
        ///     two thousand would dwarf a level 20 pool); flat health from gear and gems stays.
        /// </summary>
        public static int SyncGearHealth(int gearHealth, int armorHealth, int level) =>
            Math.Max(0, gearHealth - armorHealth + Math.Min(armorHealth, CombatMath.ArmorHealth(MaxArmor(level))));

        /// <summary>Level sync on a floor: skill levels, the health pool and gear capped at the floor's sync level.</summary>
        public static void Sync(CombatSheet sheet, int floor, int gearHealth = 0)
        {
            var level = SyncLevel(floor);
            sheet.CapLevels(level, gearHealth);
            CapGear(sheet, level);
        }

        /// <summary>The combat level a floor is made for: ten per floor, so floor 100 is made for level 1,000.</summary>
        public static int Level(int floor) => Math.Max(1, floor * 10);

        // gear an enemy of a combat level has, fitted to today's island enemies (their definitions after the enemy gear
        // tuning: armour 70 + 2 per level, power 15 + 0.22, accuracy 12 + 0.18 per level)
        public static int ArmorAt(int level) => 70 + 2 * level;
        public static int PowerAt(int level) => 15 + (int)(0.22 * level);
        public static int AccuracyAt(int level) => 12 + (int)(0.18 * level);

        /// <summary>
        ///     One of the part's monsters under a fight's pressure (<see cref="TowerPressure"/>): its health by the
        ///     pressure, everything else as always. <paramref name="helper"/>: one a guardian called.
        /// </summary>
        public static EnemyDefinition Enemy(TowerFloorDefinition floor, TowerEnemy enemy, double pressure, bool helper = false)
        {
            var d = Enemy(floor, enemy);
            var scale = helper ? TowerPressure.HelperHealth(floor.Floor, pressure) : TowerPressure.MonsterHealth(floor.Floor, pressure);
            d.Health = TowerPressure.Scale(d.Health, scale);
            // a small group's mercy reaches the guardian's helpers too: they strike as softly as it does
            if (helper && pressure < 1) d.DamageScale = TowerPressure.BossBlows(floor.Floor, pressure);
            return d;
        }

        /// <summary>
        ///     The part's guardian under a fight's pressure: its health by the pressure and its blows a little; a full
        ///     group of fresh players (pressure 1) meets the fixed numbers, and never less.
        /// </summary>
        public static EnemyDefinition Boss(TowerFloorDefinition floor, double pressure)
        {
            var d = Boss(floor);
            d.Health = TowerPressure.Scale(d.Health, TowerPressure.BossHealth(floor.Floor, pressure));
            d.DamageScale = TowerPressure.BossBlows(floor.Floor, pressure);
            // since knocked out players come back (TowerRecovery): harder blows, and a floor boss that lasts; a
            // group smaller than the part's suggested one meets a share of both
            var share = TowerPressure.FuryShare(pressure * BossGroup(floor) / SuggestedGroup);
            d.DamageScale *= 1 + (TowerPressure.Fury(floor.Floor, floor.IsFloorBoss) - 1) * share;
            if (floor.IsFloorBoss) d.Health = TowerPressure.Scale(d.Health, 1 + (TowerPressure.FloorBossPool - 1) * share);
            return d;
        }

        /// <summary>One of the floor's monsters at the floor's level: its look and style from the game, its numbers from here.</summary>
        public static EnemyDefinition Enemy(TowerFloorDefinition floor, TowerEnemy enemy)
        {
            var band = BandOf(floor.Number);
            var level = Math.Max(1, floor.Level + enemy.LevelOffset);
            var d = new EnemyDefinition
            {
                Id = "tower-" + enemy.Id,
                Name = enemy.Name,
                Area = "Tower " + Short(floor.Number),
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

        /// <summary>
        ///     The part's guardian (a mini boss, or the floor boss on the floor's last part) for the suggested group:
        ///     fixed numbers for its part, so climbing is a real progression.
        /// </summary>
        public static EnemyDefinition Boss(TowerFloorDefinition floor)
        {
            var band = BandOf(floor.Number);
            var b = floor.Boss;
            var level = Math.Max(1, (int)Math.Round(floor.Level * b.LevelScale));
            var d = new EnemyDefinition
            {
                Id = "tower-boss-" + floor.Number,
                Name = b.Name,
                Area = "Tower " + Short(floor.Number),
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
            new TowerBand
            {
                Number = 2, FirstFloor = 11, LastFloor = 20,
                Name = "The Frozen Vault",
                Biome = "ice",
                Description = "Above the halls the stone turns to ice. Raiders, wraiths and worse sleep in the cold, and every blow bites with frost.",
                Element = Element.Ice, ElementShare = 0.3,
                Resistance = new ElementValues { Ice = 0.5, Fire = -0.25, Lightning = -0.1 },
                Town = "Rimeholt",
            },
            new TowerBand
            {
                Number = 3, FirstFloor = 21, LastFloor = 30,
                Name = "The Storm Spire",
                Biome = "storm",
                Description = "Above the ice the tower breaks through the clouds. Sky pirates moor on the wind, drakes ride the storm, and every blow crackles with lightning.",
                Element = Element.Lightning, ElementShare = 0.3,
                Resistance = new ElementValues { Lightning = 0.5, Poison = -0.25, Ice = -0.1 },
                Town = "Galewharf",
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
            // the undead of the Dark Fortress (2026-11-23, the game's docs/dark-fortress-design.md): Synty's POLYGON Dark
            // Fortress characters on the humanoid enemy animations, in the crypt and the marsh
            var risen = new TowerEnemy { Id = "crypt-risen", Name = "Risen Dead", Model = "df-undead", Family = EnemyFamily.Undead };
            var risenSoldier = new TowerEnemy { Id = "crypt-risen-soldier", Name = "Risen Soldier", Model = "df-undead-soldier", Family = EnemyFamily.Undead, LevelOffset = 1, AttackInterval = 1.1 };
            var corpse = new TowerEnemy { Id = "crypt-corpse", Name = "Shambling Corpse", Model = "df-corpse", Family = EnemyFamily.Undead, LevelOffset = -1, AttackInterval = 1.3 };
            var plague = new TowerEnemy { Id = "marsh-plague", Name = "Plague Walker", Model = "df-plague", Family = EnemyFamily.Undead, LevelOffset = 1, AttackInterval = 1.1 };
            var cryptWraith = new TowerEnemy { Id = "crypt-wraith", Name = "Crypt Wraith", Model = "df-wraith", Family = EnemyFamily.Spirit, Style = AttackStyle.Magic, LevelOffset = 1, AttackInterval = 1.4 };
            var graveKnight = new TowerEnemy { Id = "crypt-grave-knight", Name = "Grave Knight", Model = "df-grave-knight", Family = EnemyFamily.Undead, LevelOffset = 3, AttackInterval = 1.2 };
            // the fortress's living servants (round 2): elites a few levels above the floor
            var bogApothecary = new TowerEnemy { Id = "marsh-apothecary", Name = "Bog Apothecary", Model = "df-apothecary", Family = EnemyFamily.Humanoid, Style = AttackStyle.Magic, LevelOffset = 1, AttackInterval = 1.3 };
            var antlerReaver = new TowerEnemy { Id = "hall-antler-reaver", Name = "Antlered Reaver", Model = "df-barbarian", Family = EnemyFamily.Humanoid, LevelOffset = 2, AttackInterval = 1.1 };
            var fallenInquisitor = new TowerEnemy { Id = "crypt-fallen-inquisitor", Name = "Fallen Inquisitor", Model = "df-inquisitor", Family = EnemyFamily.Humanoid, Style = AttackStyle.Magic, LevelOffset = 3, AttackInterval = 1.3 };

            // The parts' objectives (docs/tower-worlds-proposal.md section 12): two kill objectives each, the simplest
            // case, and on a few parts of every floor a gather or a find that one of the floor's townspeople asks for
            // (after the boss in the call). The amounts set the pace: see the table in section 12.
            var list = new List<TowerFloorDefinition>
            {
                Part(1, "The Burrows", "Something has been digging under the first hall. A lot of somethings.",
                    new[] { bunny, goblin }, Defeat(bunny, 240), Defeat(goblin, 190),
                    new TowerBoss { Name = "Nibbles the Vicious", Model = "home-bunny", Scale = 4f, Family = EnemyFamily.Beast, Line = "Run away! Run away!" }),
                Part(2, "Goblin Market", "The goblins trade in stolen boots and very old cheese.",
                    new[] { goblin, bunny }, Defeat(goblin, 360), Defeat(bunny, 290),
                    new TowerBoss { Name = "Warchief Grubnose", Model = "ravebridge-goblin", Scale = 2.2f, Family = EnemyFamily.Humanoid, PowerScale = 1.15 },
                    Find("boots", "Stolen Boot", "Stolen Boots", 3, "mossbeard", "The goblins trade in boots, and half of them came off the feet of Old Mossbeard's guests. He wants them back before anyone goes home barefoot.")),
                Part(3, "The Nest", "Eggs the size of barrels, and the mothers who laid them.",
                    new[] { greenMetalon, goblin, bogApothecary }, Defeat(greenMetalon, 240), Defeat(goblin, 190),
                    new TowerBoss { Name = "The Brood Mother", Model = "home-metalon-green", Scale = 2.6f, Family = EnemyFamily.Insect, Element = Element.Poison, ElementShare = 0.5, SpitEvery = 4, AddsCount = 4 },
                    Gather("herbs", "herbs", 1100, "bramble", "Bramble brews antivenom for everyone the Brood Mother's children bite. She takes any herbs and greens the stream brings in.", "Gathering", "Farming")),
                Part(4, "Hall of Bones", "The roots grew through the old graves, and the graves did not like it.",
                    new[] { risen, skeleton, risenSoldier }, Defeat(skeleton, 320), Defeat(risen, 260),
                    // the Dark Fortress's Necromancer (2026-11-23, was the Rootbound King): a caster who raises the dead,
                    // three Risen Dead at three quarters, half and a quarter of its health
                    new TowerBoss { Name = "Mortimer Bonewhistle", Model = "df-necromancer", Scale = 2.2f, Family = EnemyFamily.Humanoid, Style = AttackStyle.Magic, PowerScale = 0.6, HealthPool = 0.9, AddsAt = new[] { 0.75, 0.5, 0.25 }, AddsCount = 3, SlamEvery = 0, SpitEvery = 5, Line = "Rise, my darlings. Supper has arrived.",
                        Trophies = new[] { "Bonewhistle Staff", "Grinning Skull" } }),
                Part(5, "Thornwood", "The trees here have thorns as long as swords, and so do the Metalons.",
                    new[] { purpleMetalon, goblin, antlerReaver }, Defeat(purpleMetalon, 270), Defeat(goblin, 220),
                    new TowerBoss { Name = "Old Thornback", Model = "home-metalon-purple", Scale = 2.8f, Family = EnemyFamily.Insect, HealthPool = 1.1 },
                    Gather("timber", "timber", 1200, "elm", "The thorns have eaten the stair to the pasture. Warden Elm wants timber to lay a new one over them.", "Woodcutting")),
                Part(6, "The Pasture", "A peaceful meadow with cows. The cows are watching you.",
                    new[] { cow, bunny }, Defeat(cow, 550), Defeat(bunny, 425),
                    new TowerBoss { Name = "The Moo-tant", Model = "home-cow", Scale = 3.2f, Family = EnemyFamily.Beast, Element = Element.Poison, ElementShare = 0.6, SpitEvery = 4, Line = "Moooo." },
                    Find("carrots", "Lost Carrot", "Lost Carrots", 4, "pip", "Pip left carrots all over the meadow in case Nibbles came back. The cows have not found them all yet.")),
                Part(7, "Goblin Keep", "Every goblin you met so far had a cousin. They all live here.",
                    new[] { goblin, skeleton, graveKnight }, Defeat(goblin, 300), Defeat(skeleton, 240),
                    // the Dark Fortress's Colossus (2026-11-23, was King Grubnose the Second): slow, heavy swings and a
                    // ground slam every third one; the keep's goblins come running at half its health
                    new TowerBoss { Name = "Big Gorp, the Keep Colossus", Model = "df-colossus", Scale = 2.4f, Family = EnemyFamily.Humanoid, PowerScale = 1.15, HealthPool = 1.0, AttackInterval = 3, SlamEvery = 3, SlamShare = 0.6, AddsAt = new[] { 0.5 }, AddsCount = 4, Line = "King Grubnose says: no visitors.",
                        Trophies = new[] { "Colossus Cleaver", "Keepward Shield" } },
                    Gather("iron", "iron ore", 1350, "elm", "The keep's gate is goblin work and will not hold. Warden Elm wants iron to bar it behind the climbers.", "Mining")),
                Part(8, "Blighted Grove", "The rot starts here. The red Metalons carry it, and the dead walk in it.",
                    new[] { plague, redMetalon, purpleMetalon }, Defeat(redMetalon, 400), Defeat(plague, 320),
                    // the Dark Fortress's Plague Lord (2026-11-23, was Redshell the Blighted): poison clouds every third
                    // swing (the spit's marked circle), and its Plague Walkers at half its health
                    new TowerBoss { Name = "Lord Pestilus", Model = "df-plague-lord", Scale = 2.3f, Family = EnemyFamily.Undead, Style = AttackStyle.Magic, PowerScale = 0.9, Element = Element.Poison, ElementShare = 0.6, HealthPool = 1.1, SpitEvery = 3, SpitShare = 1.3, AddsAt = new[] { 0.5 }, Line = "Breathe deep, little ones. It is good for you.",
                        Trophies = new[] { "Plague Mitre", "Blightbone Staff" } },
                    Find("samples", "Blight Sample", "Blight Samples", 3, "bramble", "Bramble wants to know what the rot is before it reaches her garden. Somebody has to bring her a few jars of it.")),
                // the Behemoth since 2026-11-23 (was the Gardener, a big skeleton): the same fight and numbers, a body
                // that fits the rot, a name and a line of its own, and trophies
                Part(9, "The Deep Roots", "Everything that lives in the halls comes down here to hide, and something down here tends to it.",
                    new[] { corpse, cryptWraith, redMetalon }, Defeat(corpse, 340), Defeat(cryptWraith, 270),
                    new TowerBoss { Name = "Old Rotbelly", Model = "df-behemoth", Scale = 2.3f, Family = EnemyFamily.Undead, Style = AttackStyle.Magic, PowerScale = 0.6, HealthPool = 0.9, Element = Element.Poison, ElementShare = 0.5, SpitEvery = 4, AddsAt = new[] { 0.66, 0.33 }, Line = "Fresh compost! Come closer. Closer.",
                        Trophies = new[] { "Rotbelly Bonesaw", "Rotreaper" } },
                    Gather("provisions", "provisions", 1500, "mossbeard", "Nobody faces what sleeps above on an empty stomach. Old Mossbeard is cooking for the whole climb and needs fish and crops.", "Fishing", "Farming")),
                Part(10, "The Verdant Throne", "At the top of the halls, on a throne of moss, sleeps the one who made it all grow.",
                    new[] { redMetalon, purpleMetalon, fallenInquisitor }, Defeat(redMetalon, 450), Defeat(purpleMetalon, 350),
                    new TowerBoss { Name = "Viridia, the Verdant Wyrm", Group = FloorBossGroup, Model = "ironhill-dragon-green", Scale = 1.6f, Family = EnemyFamily.Dragon, Element = Element.Poison, ElementShare = 0.7, HealthPool = 1.25, PowerScale = 1.2, AttackInterval = 3, SpitEvery = 3, AddsAt = new[] { 0.75, 0.5, 0.25 }, AddsCount = 4, EnrageSeconds = 420, Line = "You climbed all this way to become compost." }),
            };

            // the second band, the Frozen Vault (2026-11-10): ice, weak to fire
            var raider = new TowerEnemy { Id = "frost-raider", Name = "Frost Raider", Model = "iceheim-viking", Family = EnemyFamily.Humanoid };
            var hare = new TowerEnemy { Id = "snow-hare", Name = "Snow Hare", Model = "home-bunny", Family = EnemyFamily.Beast, LevelOffset = -2, AttackInterval = 1.1 };
            var frozenSkeleton = new TowerEnemy { Id = "frozen-skeleton", Name = "Frozen Skeleton", Model = "ravebridge-skeleton", Family = EnemyFamily.Undead };
            var wraith = new TowerEnemy { Id = "frost-wraith", Name = "Frost Wraith", Model = "ravebridge-tormented-soul", Family = EnemyFamily.Spirit, Style = AttackStyle.Magic, LevelOffset = 1, AttackInterval = 1.4 };
            var quackling = new TowerEnemy { Id = "quackling", Name = "Quackling", Model = "ironhill-lord-quackers", Family = EnemyFamily.Beast, LevelOffset = -1, AttackInterval = 1.2, Scale = 0.4f };
            var thane = new TowerEnemy { Id = "raider-thane", Name = "Raider Thane", Model = "iceheim-viking-chief", Family = EnemyFamily.Humanoid, LevelOffset = 3, AttackInterval = 1.2 };
            var drake = new TowerEnemy { Id = "frost-drake", Name = "Frost Drake", Model = "ironhill-dragon-white", Family = EnemyFamily.Dragon, LevelOffset = 3, AttackInterval = 1.3 };
            var ronin = new TowerEnemy { Id = "frozen-ronin", Name = "Frozen Ronin", Model = "kyojin-samurai", Family = EnemyFamily.Humanoid, LevelOffset = 2 };
            var frostCow = new TowerEnemy { Id = "frostbitten-cow", Name = "Frostbitten Cow", Model = "home-cow", Family = EnemyFamily.Beast, LevelOffset = -1, AttackInterval = 1.5 };

            list.AddRange(new[]
            {
                Part(11, "The Frostgate", "The stairs come out into the cold. Somebody has built a gate here, and somebody else is raiding it.",
                    new[] { raider, hare }, Defeat(raider, 775), Defeat(hare, 625),
                    new TowerBoss { Name = "Jarl Rimebeard", Model = "iceheim-viking-chief", Scale = 2.2f, Family = EnemyFamily.Humanoid, PowerScale = 1.1, Line = "Shut the door! You are letting the warm in." }),
                Part(12, "Hare Hollow", "Burrows in the snow, and the hares have heard what happened on the first floor.",
                    new[] { hare, raider }, Defeat(hare, 800), Defeat(raider, 650),
                    new TowerBoss { Name = "Snowball, Nibbles' Big Sister", Model = "home-bunny", Scale = 4.6f, Family = EnemyFamily.Beast, PowerScale = 1.1, Line = "You defeated my little brother. Prepare to be nibbled." },
                    Find("feathers", "Duck Feather", "Duck Feathers", 3, "ylva", "Ylva's duck flew up the tower and left feathers behind. Each one found tells her he came this way.")),
                Part(13, "The Bone Glacier", "The ice is full of bones. Some of them are still moving.",
                    new[] { frozenSkeleton, wraith }, Defeat(frozenSkeleton, 675), Defeat(wraith, 550),
                    new TowerBoss { Name = "The Frozen Warden", Model = "ravebridge-skeleton", Scale = 2.6f, Family = EnemyFamily.Undead, Style = AttackStyle.Magic, PowerScale = 0.7, Element = Element.Ice, ElementShare = 0.5, SpitEvery = 4 },
                    Gather("coal", "coal and ore", 2000, "hilda", "The cold up here cracks steel. Hilda needs coal and ore to keep the forge lit and the climbers' blades whole.", "Mining")),
                Part(14, "Hall of Whispers", "Every sound comes back colder than it left.",
                    new[] { wraith, frozenSkeleton }, Defeat(wraith, 875), Defeat(frozenSkeleton, 700),
                    new TowerBoss { Name = "The Wailing Matron", Model = "ravebridge-tormented-soul", Scale = 2.6f, Family = EnemyFamily.Spirit, Style = AttackStyle.Magic, PowerScale = 0.7, HealthPool = 0.95, AddsAt = new[] { 0.66, 0.33 }, Line = "Shhhhh." }),
                Part(15, "The Frozen Pond", "A pond frozen solid, and on it a lot of very small ducks with very sharp beaks.",
                    new[] { quackling, hare }, Defeat(quackling, 725), Defeat(hare, 575),
                    new TowerBoss { Name = "Emperor Quackers", Model = "ironhill-lord-quackers", Scale = 1.5f, Family = EnemyFamily.Beast, HealthPool = 1.1, SpitEvery = 4, Line = "QUACK." },
                    Gather("fish", "fish", 2150, "tove", "Old Tove says the ducks have scared every fish out of the pond. He needs fish from anywhere to stock Rimeholt for the winter.", "Fishing")),
                Part(16, "Raiders' Mead Hall", "The raiders' long hall. The mead is warm, the welcome is not.",
                    new[] { raider, thane }, Defeat(raider, 925), Defeat(thane, 750),
                    new TowerBoss { Name = "Sigrid the Unthawed", Model = "iceheim-viking-chief", Scale = 2.5f, Family = EnemyFamily.Humanoid, PowerScale = 1.2, Line = "Mead first. Then I break you." },
                    Find("barrels", "Mead Barrel", "Mead Barrels", 4, "gunnhild", "The raiders drink Big Gunnhild's mead, and they did not pay for it. She wants her barrels back, full or not.")),
                Part(17, "The Drake Roost", "Nests of ice on every ledge. The drakes do not share.",
                    new[] { drake, quackling }, Defeat(drake, 775), Defeat(quackling, 625),
                    new TowerBoss { Name = "Hoarfang", Model = "ironhill-dragon-white", Scale = 1.3f, Family = EnemyFamily.Dragon, Element = Element.Ice, ElementShare = 0.6, HealthPool = 1.1, AttackInterval = 3, SpitEvery = 3 },
                    Gather("firewood", "firewood", 2300, "gunnhild", "Drake breath has frozen every hearth below the roost. Big Gunnhild needs firewood to thaw the mead hall out.", "Woodcutting")),
                Part(18, "The Shiver Mines", "Old mines cut into the ice, and something frozen in the deepest shaft is waking up.",
                    new[] { ronin, frozenSkeleton }, Defeat(ronin, 1000), Defeat(frozenSkeleton, 800),
                    new TowerBoss { Name = "The Thawed Ronin", Model = "kyojin-samurai", Scale = 2.3f, Family = EnemyFamily.Humanoid, PowerScale = 1.25, Line = "A thousand winters I waited for a worthy duel. You will have to do." },
                    Find("lanterns", "Miner's Lantern", "Miner's Lanterns", 4, "hilda", "Hilda's grandfather dug these mines and never came out. His crew's lanterns are still down there somewhere.")),
                Part(19, "Cold Storage", "Somebody keeps their cows up here. To keep them fresh. They did not stay fresh.",
                    new[] { frostCow, drake }, Defeat(frostCow, 825), Defeat(drake, 650),
                    new TowerBoss { Name = "The Moo-tant, Refrozen", Model = "home-cow", Scale = 3.4f, Family = EnemyFamily.Beast, Element = Element.Ice, ElementShare = 0.6, SpitEvery = 4, Line = "Mooooo. Brrrr." },
                    Gather("stew", "food for the stew", 2450, "tove", "Before the last climb of the vault, Old Tove makes his stew. It takes a lot of crops, and anything that grows wild.", "Farming", "Gathering")),
                Part(20, "The Rime Throne", "At the top of the vault, on a throne of ice, the cold itself has a name.",
                    new[] { drake, thane }, Defeat(drake, 1050), Defeat(thane, 850),
                    new TowerBoss { Name = "Glacius, the Winter Wyrm", Group = FloorBossGroup, Model = "ironhill-dragon-white", Scale = 1.7f, Family = EnemyFamily.Dragon, Element = Element.Ice, ElementShare = 0.7, HealthPool = 1.25, PowerScale = 1.2, AttackInterval = 3, SpitEvery = 3, AddsAt = new[] { 0.75, 0.5, 0.25 }, AddsCount = 4, EnrageSeconds = 420, Line = "Climb all you like. Up here, everything stops." }),
            });

            // the third band, the Storm Spire (2026-11-19): lightning, weak to poison; sky pirates, storm ninjas, the arena
            // of winds and the drakes that ride the storm
            var skyPirate = new TowerEnemy { Id = "sky-pirate", Name = "Sky Pirate", Model = "away-pirate", Family = EnemyFamily.Humanoid };
            var staticHare = new TowerEnemy { Id = "static-hare", Name = "Static Hare", Model = "home-bunny", Family = EnemyFamily.Beast, LevelOffset = -2, AttackInterval = 1.1 };
            var stormNinja = new TowerEnemy { Id = "storm-ninja", Name = "Storm Ninja", Model = "kyojin-ninja", Family = EnemyFamily.Humanoid, LevelOffset = 1, AttackInterval = 0.9 };
            var wisp = new TowerEnemy { Id = "storm-wisp", Name = "Storm Wisp", Model = "ravebridge-tormented-soul", Family = EnemyFamily.Spirit, Style = AttackStyle.Magic, LevelOffset = 1, AttackInterval = 1.4 };
            var duckling = new TowerEnemy { Id = "squall-duckling", Name = "Squall Duckling", Model = "ironhill-lord-quackers", Family = EnemyFamily.Beast, LevelOffset = -1, AttackInterval = 1.2, Scale = 0.4f };
            var corsair = new TowerEnemy { Id = "storm-corsair", Name = "Storm Corsair", Model = "mangrove-pirate-captain", Family = EnemyFamily.Humanoid, LevelOffset = 3, AttackInterval = 1.2 };
            var gladiator = new TowerEnemy { Id = "thunder-gladiator", Name = "Thunder Gladiator", Model = "atria-gladiator", Family = EnemyFamily.Humanoid, LevelOffset = 2, AttackInterval = 1.2 };
            var sentinel = new TowerEnemy { Id = "gale-sentinel", Name = "Gale Sentinel", Model = "eldara-elven-soldier", Family = EnemyFamily.Humanoid, LevelOffset = 2 };
            var stormDrake = new TowerEnemy { Id = "storm-drake", Name = "Storm Drake", Model = "ironhill-dragon-blue", Family = EnemyFamily.Dragon, LevelOffset = 3, AttackInterval = 1.3 };
            var chargedCow = new TowerEnemy { Id = "charged-cow", Name = "Charged Cow", Model = "home-cow", Family = EnemyFamily.Beast, LevelOffset = -1, AttackInterval = 1.5 };

            list.AddRange(new[]
            {
                Part(21, "The Cloud Docks", "The stairs come out above the clouds, onto a harbor of planks and rope. The ships moored here are not friendly.",
                    new[] { skyPirate, staticHare }, Defeat(skyPirate, 1150), Defeat(staticHare, 900),
                    new TowerBoss { Name = "Captain Gale Blackwind", Model = "away-pirate-captain", Scale = 2.2f, Family = EnemyFamily.Humanoid, PowerScale = 1.1, Line = "Welcome aboard. Mind the drop." }),
                Part(22, "The Static Burrows", "Hares up here have fur that crackles. The family resemblance is shocking.",
                    new[] { staticHare, skyPirate }, Defeat(staticHare, 1150), Defeat(skyPirate, 925),
                    new TowerBoss { Name = "Zap, Nibbles' Shocking Cousin", Model = "home-bunny", Scale = 4.8f, Family = EnemyFamily.Beast, PowerScale = 1.1, Element = Element.Lightning, ElementShare = 0.5, Line = "The family sends its regards. All of it." },
                    Find("wool", "Tuft of Wool", "Tufts of Wool", 4, "tessa", "Woolly floated through here on his cloud and the hares kept what he shed. Tessa wants every tuft.")),
                Part(23, "The Wind Bridges", "Stone bridges between drifting rocks, and on every one of them someone who wants you off it.",
                    new[] { stormNinja, skyPirate }, Defeat(stormNinja, 950), Defeat(skyPirate, 775),
                    new TowerBoss { Name = "Master Kaze", Model = "kyojin-ninja", Scale = 2.3f, Family = EnemyFamily.Humanoid, PowerScale = 1.15, AttackInterval = 2, Line = "You cannot hit the wind." },
                    Gather("planks", "planks", 2900, "brisa", "Half the bridges end in mid-air. Captain Brisa wants timber to plank them before somebody walks off one.", "Woodcutting")),
                Part(24, "The Wisp Gallery", "A long hall inside the spire where the lightning lives. It hums. It hums back.",
                    new[] { wisp, stormNinja }, Defeat(wisp, 1250), Defeat(stormNinja, 975),
                    new TowerBoss { Name = "Mother Squall", Model = "ravebridge-tormented-soul", Scale = 2.6f, Family = EnemyFamily.Spirit, Style = AttackStyle.Magic, PowerScale = 0.7, HealthPool = 0.95, Element = Element.Lightning, ElementShare = 0.5, SpitEvery = 4, AddsAt = new[] { 0.66, 0.33 }, Line = "Listen. The storm is singing." },
                    Find("bottles", "Lightning Bottle", "Lightning Bottles", 4, "volt", "Tinker Volt left bottles in the gallery to catch the wisps' sparks. He would like them back, carefully.")),
                Part(25, "The Duck Deck", "A ship's deck moored on a cloud, crewed entirely by ducks. They have a captain.",
                    new[] { duckling, corsair }, Defeat(duckling, 1000), Defeat(corsair, 800),
                    new TowerBoss { Name = "Captain Quackbeard", Model = "ironhill-lord-quackers", Scale = 1.5f, Family = EnemyFamily.Beast, HealthPool = 1.1, SpitEvery = 4, Line = "Arr. QUACK." },
                    Gather("rations", "rations", 3000, "hob", "A crew of ducks eats a ship's stores in a day. Skipper Hob needs fish and crops to feed the climbers instead.", "Fishing", "Farming")),
                Part(26, "The Arena of Winds", "A ring of stone open to the storm, and a crowd that came to see you lose.",
                    new[] { gladiator, sentinel }, Defeat(gladiator, 1300), Defeat(sentinel, 1050),
                    new TowerBoss { Name = "Champion Voltus", Model = "atria-gladiator", Scale = 2.4f, Family = EnemyFamily.Humanoid, PowerScale = 1.2, Line = "The crowd wants a show. Try not to disappoint them." }),
                Part(27, "The Drake Eyrie", "Nests on the floating rocks, and drakes that dive out of the thunderclouds.",
                    new[] { stormDrake, duckling }, Defeat(stormDrake, 1050), Defeat(duckling, 850),
                    new TowerBoss { Name = "Skyfang", Model = "ironhill-dragon-purple", Scale = 1.3f, Family = EnemyFamily.Dragon, Element = Element.Lightning, ElementShare = 0.6, HealthPool = 1.1, AttackInterval = 3, SpitEvery = 3 },
                    Gather("copper", "copper ore", 3150, "volt", "Tinker Volt wants lightning rods on every rock of the eyrie, so the drakes get the storm and you do not. Rods take copper.", "Mining")),
                Part(28, "The Sentinel Spire", "The last stair inside the spire, held by elves who swore the storm an oath.",
                    new[] { sentinel, gladiator }, Defeat(sentinel, 1350), Defeat(gladiator, 1100),
                    new TowerBoss { Name = "High Warden Aerin", Model = "eldara-elven-soldier", Scale = 2.3f, Family = EnemyFamily.Humanoid, Style = AttackStyle.Magic, PowerScale = 0.7, Element = Element.Lightning, ElementShare = 0.5, SpitEvery = 4, AddsAt = new[] { 0.66, 0.33 }, Line = "None pass the spire without the storm's leave." },
                    Find("stones", "Oath Stone", "Oath Stones", 5, "brisa", "The sentinels swore their oath on five stones. Captain Brisa thinks the storm will let you pass if you carry them up.")),
                Part(29, "The Lightning Pasture", "Somebody grazes cows on a cloud. The lightning found them first.",
                    new[] { chargedCow, stormDrake }, Defeat(chargedCow, 1100), Defeat(stormDrake, 875),
                    new TowerBoss { Name = "The Moo-tant, Supercharged", Model = "home-cow", Scale = 3.5f, Family = EnemyFamily.Beast, Element = Element.Lightning, ElementShare = 0.6, SpitEvery = 4, Line = "MOOOOO. Zzzt." },
                    Gather("fodder", "fodder", 3300, "tessa", "Charged cows calm down when they eat. Tessa needs hay and herbs, a lot of both.", "Farming", "Gathering")),
                Part(30, "The Eye of the Storm", "At the very top of the spire the storm turns around one point, and something lives in it.",
                    new[] { stormDrake, gladiator }, Defeat(stormDrake, 1400), Defeat(gladiator, 1150),
                    new TowerBoss { Name = "Fulmina, the Thunder Wyrm", Group = FloorBossGroup, Model = "ironhill-dragon-blue", Scale = 1.8f, Family = EnemyFamily.Dragon, Element = Element.Lightning, ElementShare = 0.7, HealthPool = 1.25, PowerScale = 1.2, AttackInterval = 3, SpitEvery = 3, AddsAt = new[] { 0.75, 0.5, 0.25 }, AddsCount = 4, EnrageSeconds = 420, Line = "You wanted to touch the sky. The sky touches back." }),
            });
            return list;
        }

        /// <summary>
        ///     The first band's layouts (Zerratar 2026-11-07: floors should look different, and some should be real
        ///     dungeons): the overgrown courtyard, a drowned marsh, and the crypt under the halls.
        /// </summary>
        public const string Overgrown = "Tower_Overgrown";
        public const string Marsh = "Tower_Marsh";
        public const string Crypt = "Tower_Crypt";

        /// <summary>The second band's layouts: a glacier valley under the open sky and the ice vault inside the tower.</summary>
        public const string Glacier = "Tower_Glacier";
        public const string FrostVault = "Tower_FrostVault";

        /// <summary>The third band's layouts: the cloud docks on a rock above the clouds, and the storm hall inside the spire.</summary>
        public const string SkyDocks = "Tower_SkyDocks";
        public const string StormSpire = "Tower_StormSpire";

        // ---- A floor's areas: the places round its town (docs/tower-worlds-proposal.md section 13) ------------------
        // A floor is a town with areas around it. An area is one layout (the courtyard, the marsh, the crypt) and the
        // parts fought there; the floor map in the game and on the website is drawn from this. Any number of layouts
        // and parts works: nothing here counts on three areas or ten parts.

        /// <summary>What players call a layout as a place on the floor map: "The Crypt".</summary>
        public static string AreaName(string layout)
        {
            switch (layout)
            {
                case Overgrown: return "The Courtyard";
                case Marsh: return "The Drowned Marsh";
                case Crypt: return "The Crypt";
                case Glacier: return "The Glacier";
                case FrostVault: return "The Ice Vault";
                case SkyDocks: return "The Sky Docks";
                case StormSpire: return "The Spire's Crown";
                case "Tower_GreatHall": return "The Great Hall";
            }
            if (string.IsNullOrEmpty(layout)) return "";
            return layout.StartsWith("Tower_") ? layout.Substring(6) : layout;
        }

        private static readonly Dictionary<int, TowerArea[]> areasByFloor = new Dictionary<int, TowerArea[]>();

        /// <summary>
        ///     The areas of a floor, in the order the climb first reaches them, each with its parts in climbing order.
        ///     Empty for a floor that does not exist.
        /// </summary>
        public static IReadOnlyList<TowerArea> AreasOf(int floor)
        {
            lock (areasByFloor)
            {
                if (areasByFloor.TryGetValue(floor, out var known)) return known;
                var band = FloorInfo(floor);
                var list = new List<TowerArea>();
                if (band != null)
                {
                    var steps = new Dictionary<string, List<int>>();
                    for (var step = band.FirstFloor; step <= band.LastFloor; step++)
                    {
                        var part = Get(step);
                        if (part == null) continue;
                        var layout = part.Layout ?? "";
                        if (!steps.TryGetValue(layout, out var of))
                        {
                            steps[layout] = of = new List<int>();
                            list.Add(new TowerArea { Floor = floor, Layout = layout, Name = AreaName(layout) });
                        }
                        of.Add(step);
                    }
                    foreach (var area in list) area.Steps = steps[area.Layout].ToArray();
                }
                return areasByFloor[floor] = list.ToArray();
            }
        }

        /// <summary>The area a step is fought in, or null when the step does not exist.</summary>
        public static TowerArea AreaOf(int step)
        {
            var part = Get(step);
            if (part == null) return null;
            foreach (var area in AreasOf(part.Floor)) if (area.Layout == (part.Layout ?? "")) return area;
            return null;
        }

        private static string LayoutOf(int number)
        {
            switch (number)
            {
                case 3: case 8: return Marsh;
                case 4: case 7: case 9: case 10: return Crypt;
                case 11: case 12: case 15: case 16: case 19: return Glacier;
                case 13: case 14: case 17: case 18: case 20: return FrostVault;
                case 21: case 22: case 23: case 25: case 27: case 29: return SkyDocks;
                case 24: case 26: case 28: case 30: return StormSpire;
                default: return Overgrown;
            }
        }

        /// <summary>
        ///     One part: its monsters, its guardian and its objectives. The two kill objectives come first (the simplest
        ///     case), then whatever else the part asks for (<see cref="Gather"/>, <see cref="Find"/>), and last, added
        ///     here, the guardian itself, which opens when the others are done.
        /// </summary>
        private static TowerFloorDefinition Part(int number, string name, string description, TowerEnemy[] enemies, TowerObjective q1, TowerObjective q2, TowerBoss boss, params TowerObjective[] more)
        {
            if (boss.Id == null) boss.Id = "boss-" + number;
            var objectives = new List<TowerObjective> { q1, q2 };
            if (more != null) objectives.AddRange(more);
            objectives.Add(new TowerObjective
            {
                Id = GuardianId,
                Kind = TowerObjectiveKind.Guardian,
                Target = boss.Id,
                Amount = 1,
                Text = "Defeat " + boss.Name,
            });
            return new TowerFloorDefinition
            {
                Number = number,
                Name = name,
                Description = description,
                Level = Level(number),
                GroupSize = SuggestedGroup,
                Layout = LayoutOf(number),
                Enemies = enemies,
                Objectives = objectives.ToArray(),
                Boss = boss,
            };
        }

        /// <summary>The group a part and its guardian are made for; scaling by pressure measures a group against it.</summary>
        public const int SuggestedGroup = 5;

        /// <summary>
        ///     The group a floor boss is made for. Its fixed numbers were set (2026-11-08) so that five players at its
        ///     level lose and eight win, so its fight is measured against eight: eight or more meet at least the fixed
        ///     numbers, and a group under eight gets the floor's mercy, which is what lets a small stream past the end
        ///     of a floor at all. (Measured against five, nine mixed bots met 30% more health and lost on floor 1, and
        ///     in the simulator neither two nor five fresh players could ever win.)
        /// </summary>
        public const int FloorBossGroup = 8;

        /// <summary>
        ///     The group a part's guardian is made for: the boss's own (<see cref="TowerBoss.Group"/>: a floor boss's
        ///     <see cref="FloorBossGroup"/>) or the part's. Pressure measures the guardian's fight against it.
        /// </summary>
        public static int BossGroup(TowerFloorDefinition floor) =>
            floor == null ? SuggestedGroup : floor.Boss != null && floor.Boss.Group > 0 ? floor.Boss.Group : Math.Max(1, floor.GroupSize);

        /// <summary>Items in a part's chest for each player: two from a floor boss and from every fifth part of a floor, one otherwise.</summary>
        public static int ChestItems(int step) => IsFloorBoss(step) || (PartOf(step) > 0 && PartOf(step) % 5 == 0) ? 2 : 1;

        /// <summary>How many characters can share a floor boss's last hit when their blows landed together.</summary>
        public const int MaxLastHits = 3;

        /// <summary>
        ///     Who gets the last hit on a floor boss (a title, nothing else), from what the game reports: the characters
        ///     whose blows landed as it fell, the one that took its last health first. Only characters who were in the
        ///     attempt count, each once, and at most <paramref name="max"/> share it: blows that land in the same moment
        ///     are a tie, and a tie is shared rather than decided by which blow the game happened to work out first.
        ///     Nobody valid means nobody gets it (it fell to thorns or a pet, or the report names no one).
        /// </summary>
        public static Guid[] LastHits(IReadOnlyList<Guid> reported, IReadOnlyCollection<Guid> participants, int max = MaxLastHits)
        {
            var result = new List<Guid>();
            if (reported == null || participants == null) return result.ToArray();
            foreach (var id in reported)
            {
                if (result.Count >= max) break;
                if (id == Guid.Empty || result.Contains(id)) continue;
                var took = false;
                foreach (var p in participants) if (p == id) { took = true; break; }
                if (took) result.Add(id);
            }
            return result.ToArray();
        }

        /// <summary>The id of every part's guardian objective.</summary>
        public const string GuardianId = "guardian";

        /// <summary>In a game's report: every kill on the part, whatever the kind (gather objectives count them too).</summary>
        public const string AllKills = "kills";

        /// <summary>
        ///     In a game's report: the heads on the part as its kills fell (<see cref="TowerPace.Heads"/>), in hundredths
        ///     ("heads:500" is five players). What each kill is worth follows from it (<see cref="TowerPace.KillWeight"/>).
        /// </summary>
        public const string Heads = "heads";

        /// <summary>Kill: defeat a number of one kind of the part's monsters, by whoever trains there.</summary>
        private static TowerObjective Defeat(TowerEnemy enemy, int amount) => new TowerObjective
        {
            Id = "defeat-" + enemy.Id,
            Kind = TowerObjectiveKind.Kill,
            Target = enemy.Id,
            Amount = amount,
            Text = "Defeat " + amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " " + Plural(enemy.Name),
        };

        /// <summary>
        ///     Gather: materials the stream's players gather anyway, wherever they train: every gathering round of one of
        ///     <paramref name="skills"/> by a player of the stream counts one (counted by the server, never taken from
        ///     anyone's bag). So that a stream with nobody training those skills is never stuck, every monster defeated
        ///     on the part counts one too (what it carried).
        /// </summary>
        private static TowerObjective Gather(string id, string what, int amount, string npc, string story, params string[] skills) => new TowerObjective
        {
            Id = "gather-" + id,
            Kind = TowerObjectiveKind.Gather,
            Target = what,
            Amount = amount,
            Skills = skills,
            Npc = npc,
            Story = story,
            Text = "Gather " + amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " " + what,
        };

        /// <summary>
        ///     Find: hidden things on the part, one at a time. They lie among the part's monsters, and players who
        ///     train there find them by themselves after a while (<see cref="FindSeconds"/>); nobody has to look for
        ///     one or click anything.
        /// </summary>
        private static TowerObjective Find(string id, string thing, string things, int amount, string npc, string story) => new TowerObjective
        {
            Id = "find-" + id,
            Kind = TowerObjectiveKind.Find,
            Target = thing,
            Amount = amount,
            Npc = npc,
            Story = story,
            Text = "Find " + amount + " " + things,
        };

        /// <summary>
        ///     Seconds of searching one hidden thing takes: a second counts in full while a player on the part stands
        ///     where its monsters are (within <see cref="FindNearMeters"/> of one of the part's monster spots), and
        ///     <see cref="FindFarShare"/> of one while the players on the part are away from them (at the arrival,
        ///     waiting, getting back up). So a group that fights finds a thing in about this time, wherever on the
        ///     part it fights. Longer higher up.
        /// </summary>
        public static int FindSeconds(int step) => 240 + 60 * Math.Max(0, FloorOf(step) - 1);
        public const double FindNearMeters = 9;
        public const double FindFarShare = 0.25;

        private static string Plural(string name)
        {
            if (name.EndsWith("y") && !name.EndsWith("ey")) return name.Substring(0, name.Length - 1) + "ies";
            // "Risen Dead", "Frozen Ronin": the same for one and many
            if (name.EndsWith("s") || name.EndsWith("Ronin") || name.EndsWith("Dead")) return name;
            return name + "s";
        }

        /// <summary>
        ///     A part's objective progress in its text form: "defeat-hall-goblin:12;gather-timber:30;find-boots:1". Every
        ///     counted objective of the part is in the result (the guardian is not: it is done when the part is
        ///     cleared); unknown ids are kept out, each count is capped at its objective's amount.
        /// </summary>
        public static Dictionary<string, int> ParseProgress(TowerFloorDefinition floor, string text)
        {
            var result = new Dictionary<string, int>();
            if (floor == null) return result;
            foreach (var q in floor.Objectives) if (q.Counted) result[q.Id] = 0;
            foreach (var kv in ParseCounts(text))
            {
                if (!result.ContainsKey(kv.Key)) continue;
                var objective = Array.Find(floor.Objectives, q => q.Id == kv.Key);
                result[kv.Key] = Math.Max(0, Math.Min(objective.Amount, kv.Value));
            }
            return result;
        }

        /// <summary>
        ///     "a:3;b:5" as counts; what is not a count is left out. A stream's saved progress can carry the part of a
        ///     kill that is not a whole one yet ("a:3.25", <see cref="ParseFractions"/>): its whole number is the count.
        /// </summary>
        public static Dictionary<string, int> ParseCounts(string text)
        {
            var result = new Dictionary<string, int>();
            foreach (var kv in ParseAmounts(text)) result[kv.Key] = (int)Math.Floor(kv.Value + 1e-9);
            return result;
        }

        /// <summary>
        ///     What a stream's saved progress carries beyond the whole numbers: "a:3.25;b:5" gives a: 0.25. A kill by a
        ///     small group counts for more than one and a big group's for less (<see cref="TowerPace"/>); the part that
        ///     is not a whole kill yet is kept here and never shown.
        /// </summary>
        public static Dictionary<string, double> ParseFractions(string text)
        {
            var result = new Dictionary<string, double>();
            foreach (var kv in ParseAmounts(text))
            {
                var fraction = kv.Value - Math.Floor(kv.Value + 1e-9);
                if (fraction >= 0.005) result[kv.Key] = fraction;
            }
            return result;
        }

        private static Dictionary<string, double> ParseAmounts(string text)
        {
            var result = new Dictionary<string, double>();
            if (string.IsNullOrWhiteSpace(text)) return result;
            foreach (var part in text.Split(';'))
            {
                var colon = part.IndexOf(':');
                if (colon <= 0) continue;
                var id = part.Substring(0, colon).Trim();
                if (id.Length == 0) continue;
                if (!double.TryParse(part.Substring(colon + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var amount)) continue;
                if (double.IsNaN(amount) || double.IsInfinity(amount) || amount < 0 || amount > int.MaxValue) continue;
                result[id] = amount;
            }
            return result;
        }

        /// <summary>
        ///     Progress in its text form. With <paramref name="fractions"/>, the part of a kill an objective carries
        ///     beyond its whole count is written with it ("a:3.25").
        /// </summary>
        public static string FormatProgress(Dictionary<string, int> progress, Dictionary<string, double> fractions = null)
        {
            var parts = new List<string>();
            foreach (var kv in progress)
            {
                var fraction = 0.0;
                if (fractions != null) fractions.TryGetValue(kv.Key, out fraction);
                parts.Add(kv.Key + ":" + (fraction >= 0.005 && fraction < 1
                    ? (kv.Value + fraction).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                    : kv.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            return string.Join(";", parts);
        }

        /// <summary>Every counted objective of the part is done: its guardian can be challenged.</summary>
        public static bool ObjectivesDone(TowerFloorDefinition floor, Dictionary<string, int> progress)
        {
            if (floor == null) return false;
            foreach (var q in floor.Objectives)
                if (q.Counted && (!progress.TryGetValue(q.Id, out var count) || count < q.Amount)) return false;
            return true;
        }

        // An amount (kills or rounds times what each is worth) added to an objective: whole numbers go to its count,
        // the rest is carried in the fractions for the next time. True when the count or what is carried changed.
        private static bool Credit(TowerObjective q, Dictionary<string, int> progress, Dictionary<string, double> fractions, double amount)
        {
            progress.TryGetValue(q.Id, out var before);
            if (amount <= 0 || before >= q.Amount) return false;
            var carried = 0.0;
            if (fractions != null) fractions.TryGetValue(q.Id, out carried);
            var total = carried + amount;
            var whole = (int)Math.Floor(total + 1e-9);
            var after = Math.Min(q.Amount, before + whole);
            var left = after >= q.Amount ? 0 : Math.Max(0, total - whole);
            if (fractions != null)
            {
                if (left >= 0.005) fractions[q.Id] = left;
                else fractions.Remove(q.Id);
            }
            progress[q.Id] = after;
            return after != before || (fractions != null && Math.Abs(left - carried) >= 0.005);
        }

        /// <summary>
        ///     What a game reports from the stream's part since its last report (<see cref="ParseCounts"/>: kills per
        ///     kill objective, <see cref="AllKills"/> for every kill, a find per find objective) added to the part's
        ///     progress. A kill objective takes its own kills, a gather objective every kill (see <see cref="Gather"/>),
        ///     a find objective one thing a report at most (they are found one at a time). No count takes more than
        ///     <paramref name="maxPerReport"/> kills a report or goes past its objective's amount. Each kill counts
        ///     <paramref name="weight"/> (<see cref="TowerPace.KillWeight"/>: more for a small group, less for a big
        ///     one); what is not a whole kill yet is carried in <paramref name="fractions"/> (without them it is
        ///     dropped). Finds are whole and unweighted. True when anything changed.
        /// </summary>
        public static bool AddReport(TowerFloorDefinition floor, Dictionary<string, int> progress, Dictionary<string, int> report, int maxPerReport,
            Dictionary<string, double> fractions = null, double weight = 1)
        {
            if (floor == null || progress == null || report == null) return false;
            var changed = false;
            foreach (var q in floor.Objectives)
            {
                if (!q.Counted) continue;
                var add = 0;
                var each = weight;
                switch (q.Kind)
                {
                    case TowerObjectiveKind.Kill: report.TryGetValue(q.Id, out add); break;
                    case TowerObjectiveKind.Gather: report.TryGetValue(AllKills, out add); break;
                    case TowerObjectiveKind.Find: report.TryGetValue(q.Id, out add); add = Math.Min(1, add); each = 1; break;
                }
                if (add <= 0) continue;
                changed |= Credit(q, progress, fractions, Math.Min(maxPerReport, add) * Math.Max(0, each));
            }
            return changed;
        }

        /// <summary>Does a gathering round of <paramref name="skill"/> (its name: "Woodcutting") count for any objective of the part.</summary>
        public static bool Gathers(TowerFloorDefinition floor, string skill)
        {
            if (floor == null || string.IsNullOrEmpty(skill)) return false;
            foreach (var q in floor.Objectives)
                if (q.Kind == TowerObjectiveKind.Gather && Array.IndexOf(q.Skills, skill) >= 0) return true;
            return false;
        }

        /// <summary>
        ///     Gathering rounds of a skill by the stream's players, anywhere, added to the part's gather objectives that
        ///     name the skill: <paramref name="rounds"/> is the rounds times what each was worth
        ///     (<see cref="TowerPace.GatherWeight"/>); what is not a whole one yet is carried in
        ///     <paramref name="fractions"/>. True when anything changed.
        /// </summary>
        public static bool AddGathered(TowerFloorDefinition floor, Dictionary<string, int> progress, string skill, double rounds, Dictionary<string, double> fractions = null)
        {
            if (floor == null || progress == null || rounds <= 0 || string.IsNullOrEmpty(skill)) return false;
            var changed = false;
            foreach (var q in floor.Objectives)
            {
                if (q.Kind != TowerObjectiveKind.Gather || Array.IndexOf(q.Skills, skill) < 0) continue;
                changed |= Credit(q, progress, fractions, rounds);
            }
            return changed;
        }
    }

    /// <summary>
    ///     A floor of the tower as players see it: a world with one biome, element and town (the town opens it), and
    ///     its parts, the steps <see cref="FirstFloor"/> to <see cref="LastFloor"/>. (The name is from when these were
    ///     called bands of ten floors.)
    /// </summary>
    public sealed class TowerBand
    {
        /// <summary>The floor's number, as shown: 1, 2, 3.</summary>
        public int Number;
        /// <summary>The step of the floor's first part.</summary>
        public int FirstFloor;
        /// <summary>The step of the floor's last part, whose guardian is the floor boss.</summary>
        public int LastFloor;
        /// <summary>How many parts the floor has.</summary>
        public int Parts => LastFloor - FirstFloor + 1;
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

    /// <summary>
    ///     One part of a floor (the name is from when parts were called floors): its place, monsters, objectives and
    ///     guardian.
    /// </summary>
    /// <summary>
    ///     One of the places round a floor's town (<see cref="Tower.AreasOf"/>): a layout and the parts fought there.
    /// </summary>
    public sealed class TowerArea
    {
        /// <summary>The floor it belongs to.</summary>
        public int Floor;
        /// <summary>Its layout scene ("Tower_Crypt").</summary>
        public string Layout = "";
        /// <summary>What players call it: "The Crypt".</summary>
        public string Name = "";
        /// <summary>The steps of its parts, in climbing order.</summary>
        public int[] Steps = Array.Empty<int>();

        /// <summary>Its first part's step: the area is reached once the stream is on it.</summary>
        public int FirstStep => Steps.Length > 0 ? Steps[0] : 0;
    }

    public sealed class TowerFloorDefinition
    {
        /// <summary>The part's step: its number counted through the whole tower, what the server stores.</summary>
        public int Number;
        /// <summary>The floor it is on and which part of it, as shown ("Floor 2, part 4").</summary>
        public int Floor => Tower.FloorOf(Number);
        public int Part => Tower.PartOf(Number);
        /// <summary>Its guardian is the floor boss: the floor's last part.</summary>
        public bool IsFloorBoss => Tower.IsFloorBoss(Number);
        public string Label => Tower.Label(Number);
        public string Name = "";
        public string Description = "";
        /// <summary>The combat level the floor is made for.</summary>
        public int Level;
        /// <summary>How many players of <see cref="Level"/> the boss is made for.</summary>
        public int GroupSize;
        /// <summary>The floor's scene in the game; floors of a band share a few layouts.</summary>
        public string Layout = "";
        public TowerEnemy[] Enemies = Array.Empty<TowerEnemy>();
        /// <summary>What the stream does to open the guardian, and last the guardian itself; all of it counts by itself from normal play.</summary>
        public TowerObjective[] Objectives = Array.Empty<TowerObjective>();
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
        /// <summary>Its size as a share of the look's (quacklings are a small Lord Quackers).</summary>
        public float Scale = 1f;
    }

    /// <summary>
    ///     The kinds of objective a part can have (docs/tower-worlds-proposal.md section 12). All of them are passive:
    ///     they count from what the stream's players do anyway, and nothing asks anyone to click or hand something in.
    /// </summary>
    public enum TowerObjectiveKind
    {
        /// <summary>Defeat a number of one kind of the part's monsters.</summary>
        Kill = 0,
        /// <summary>Materials the stream's players gather while training anywhere (counted, never taken).</summary>
        Gather = 1,
        /// <summary>Hidden things on the part that players near them find by themselves.</summary>
        Find = 2,
        /// <summary>The part's guardian: a mini boss, or the floor boss on the floor's last part.</summary>
        Guardian = 3,
        /// <summary>An item only a gate drops (v0.9.8, with the gates); no part uses it yet.</summary>
        GateItem = 4,
    }

    /// <summary>One objective of a part, for the whole stream.</summary>
    public sealed class TowerObjective
    {
        public string Id = "";
        public TowerObjectiveKind Kind;
        /// <summary>What the tower panel shows: "Defeat 300 Feral Bunnies", "Gather 250 timber", "Find 3 Stolen Boots".</summary>
        public string Text = "";
        /// <summary>
        ///     Kill: the <see cref="TowerEnemy.Id"/> to defeat. Gather: what is gathered, in words ("timber"). Find: one
        ///     of the hidden things ("Stolen Boot"). Guardian: the boss's id.
        /// </summary>
        public string Target = "";
        public int Amount;
        /// <summary>Gather: the skills whose gathering rounds count, by name ("Woodcutting", "Mining").</summary>
        public string[] Skills = Array.Empty<string>();
        /// <summary>The person of the floor's town who asks for it (<see cref="TowerNpc.Key"/>), or null.</summary>
        public string Npc;
        /// <summary>Who wants it and why, in a line (the website shows it).</summary>
        public string Story = "";
        /// <summary>Counts toward opening the guardian: everything but the guardian itself.</summary>
        public bool Counted => Kind != TowerObjectiveKind.Guardian;
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
        /// <summary>
        ///     The group its fight is made for when that is not the part's (<see cref="Tower.BossGroup"/>): a floor
        ///     boss's is <see cref="Tower.FloorBossGroup"/>; 0: the part's. Its health pool does not follow this, only
        ///     what pressure measures a group against.
        /// </summary>
        public int Group;
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

        /// <summary>
        ///     Every this many swings it spits at one player who is not holding it (the farthest, healers first): a
        ///     marked circle under them, then a hit of <see cref="SpitShare"/> to everyone in it (0: never).
        /// </summary>
        public int SpitEvery = 0;
        public double SpitShare = 1.2;

        /// <summary>
        ///     At these shares of its health it calls help: <see cref="AddsCount"/> of the floor's first monster join the
        ///     fight, and the players have to deal with them before the boss.
        /// </summary>
        public double[] AddsAt = { 0.5 };
        public int AddsCount = 3;

        /// <summary>After this many seconds of an attempt it enrages: it swings faster and harder (0: never).</summary>
        public int EnrageSeconds = 360;
        /// <summary>Its swing time and its power once enraged, as shares of what they were.</summary>
        public double EnrageInterval = 0.6;
        public double EnragePower = 1.5;

        /// <summary>
        ///     Items only this boss gives (by name; the server knows them): on the stream's first clear of its floor each
        ///     player who fought has <see cref="TowerLoot.TrophyChance"/> of one of them. Empty for none.
        /// </summary>
        public string[] Trophies = Array.Empty<string>();
    }

    /// <summary>
    ///     A town between the bands (docs/tower-design.md, section 4): a calm place with no fighting, the first one at the
    ///     tower's foot. Its people talk, give each player their own quest chain (the server's QuestChains, by key) and
    ///     tell rumours of the floors above.
    /// </summary>
    public sealed class TowerTownDefinition
    {
        public string Key = "";
        public string Name = "";
        /// <summary>The band it opens: its town is reachable once the floor before the band is cleared (band 1: always).</summary>
        public int Band;
        public string Layout = "";
        public string Description = "";
        public TowerNpc[] People = Array.Empty<TowerNpc>();
    }

    public sealed class TowerNpc
    {
        /// <summary>Who they are in the game's scene and texts: "elm".</summary>
        public string Key = "";
        public string Name = "";
        public string Role = "";
        /// <summary>The quest chain they give (QuestChains on the server), or null for none.</summary>
        public string Chain;
    }
}
