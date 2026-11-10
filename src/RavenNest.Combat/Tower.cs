using System;
using System.Collections.Generic;

namespace RavenNest.Combat
{
    /// <summary>
    ///     The tower (the game's docs/tower-design.md): a climb of floors that a stream makes together. Every floor has
    ///     a few stream quests (defeat its monsters) and a boss; the boss opens when the quests are done and can be
    ///     tried as often as the stream likes. Floors come in bands of ten, each band its own biome, element and town.
    ///     Shared by the server (progress, rewards), the game (the floor's enemies and boss, the quest text) and the
    ///     simulator (tuning the bosses). Floors 1 to 20 (two bands) so far; more bands keep coming.
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

        /// <summary>The town a stream can visit: the one of the band its current floor is in.</summary>
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
                    new TowerBoss { Name = "The Brood Mother", Model = "home-metalon-green", Scale = 2.6f, Family = EnemyFamily.Insect, Element = Element.Poison, ElementShare = 0.5, SpitEvery = 4, AddsCount = 4 }),
                Floor(4, "Hall of Bones", "The roots grew through the old graves, and the graves did not like it.",
                    new[] { skeleton, greenMetalon }, Defeat(skeleton, 40), Defeat(greenMetalon, 30),
                    new TowerBoss { Name = "The Rootbound King", Model = "ravebridge-skeleton", Scale = 2.3f, Family = EnemyFamily.Undead, PowerScale = 1.1 }),
                Floor(5, "Thornwood", "The trees here have thorns as long as swords, and so do the Metalons.",
                    new[] { purpleMetalon, goblin }, Defeat(purpleMetalon, 40), Defeat(goblin, 30),
                    new TowerBoss { Name = "Old Thornback", Model = "home-metalon-purple", Scale = 2.8f, Family = EnemyFamily.Insect, HealthPool = 1.1 }),
                Floor(6, "The Pasture", "A peaceful meadow with cows. The cows are watching you.",
                    new[] { cow, bunny }, Defeat(cow, 40), Defeat(bunny, 40),
                    new TowerBoss { Name = "The Moo-tant", Model = "home-cow", Scale = 3.2f, Family = EnemyFamily.Beast, Element = Element.Poison, ElementShare = 0.6, SpitEvery = 4, Line = "Moooo." }),
                Floor(7, "Goblin Keep", "Every goblin you met so far had a cousin. They all live here.",
                    new[] { goblin, skeleton }, Defeat(goblin, 50), Defeat(skeleton, 35),
                    new TowerBoss { Name = "King Grubnose the Second", Model = "ravebridge-goblin", Scale = 2.6f, Family = EnemyFamily.Humanoid, PowerScale = 1.2, Line = "My brother was the warchief. I am the king." }),
                Floor(8, "Blighted Grove", "The rot starts here. The red Metalons carry it.",
                    new[] { redMetalon, purpleMetalon }, Defeat(redMetalon, 40), Defeat(purpleMetalon, 40),
                    new TowerBoss { Name = "Redshell the Blighted", Model = "home-metalon-red", Scale = 3f, Family = EnemyFamily.Insect, Element = Element.Poison, ElementShare = 0.6, HealthPool = 1.1, SpitEvery = 3 }),
                Floor(9, "The Deep Roots", "Everything that lives in the halls comes down here to hide.",
                    new[] { redMetalon, skeleton, goblin }, Defeat(redMetalon, 45), Defeat(skeleton, 45),
                    new TowerBoss { Name = "The Gardener", Model = "ravebridge-skeleton", Scale = 2.8f, Family = EnemyFamily.Undead, Style = AttackStyle.Magic, PowerScale = 0.6, HealthPool = 0.9, Element = Element.Poison, ElementShare = 0.5, SpitEvery = 4, AddsAt = new[] { 0.66, 0.33 }, Line = "Everything grows. Even you, in time." }),
                Floor(10, "The Verdant Throne", "At the top of the halls, on a throne of moss, sleeps the one who made it all grow.",
                    new[] { redMetalon, purpleMetalon }, Defeat(redMetalon, 50), Defeat(purpleMetalon, 50),
                    new TowerBoss { Name = "Viridia, the Verdant Wyrm", Model = "ironhill-dragon-green", Scale = 1.6f, Family = EnemyFamily.Dragon, Element = Element.Poison, ElementShare = 0.7, HealthPool = 1.25, PowerScale = 1.2, AttackInterval = 3, SpitEvery = 3, AddsAt = new[] { 0.75, 0.5, 0.25 }, AddsCount = 4, EnrageSeconds = 420, Line = "You climbed all this way to become compost." }),
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
                Floor(11, "The Frostgate", "The stairs come out into the cold. Somebody has built a gate here, and somebody else is raiding it.",
                    new[] { raider, hare }, Defeat(raider, 35), Defeat(hare, 30),
                    new TowerBoss { Name = "Jarl Rimebeard", Model = "iceheim-viking-chief", Scale = 2.2f, Family = EnemyFamily.Humanoid, PowerScale = 1.1, Line = "Shut the door! You are letting the warm in." }),
                Floor(12, "Hare Hollow", "Burrows in the snow, and the hares have heard what happened on the first floor.",
                    new[] { hare, raider }, Defeat(hare, 45), Defeat(raider, 25),
                    new TowerBoss { Name = "Snowball, Nibbles' Big Sister", Model = "home-bunny", Scale = 4.6f, Family = EnemyFamily.Beast, PowerScale = 1.1, Line = "You defeated my little brother. Prepare to be nibbled." }),
                Floor(13, "The Bone Glacier", "The ice is full of bones. Some of them are still moving.",
                    new[] { frozenSkeleton, wraith }, Defeat(frozenSkeleton, 40), Defeat(wraith, 30),
                    new TowerBoss { Name = "The Frozen Warden", Model = "ravebridge-skeleton", Scale = 2.6f, Family = EnemyFamily.Undead, Style = AttackStyle.Magic, PowerScale = 0.7, Element = Element.Ice, ElementShare = 0.5, SpitEvery = 4 }),
                Floor(14, "Hall of Whispers", "Every sound comes back colder than it left.",
                    new[] { wraith, frozenSkeleton }, Defeat(wraith, 45), Defeat(frozenSkeleton, 35),
                    new TowerBoss { Name = "The Wailing Matron", Model = "ravebridge-tormented-soul", Scale = 2.6f, Family = EnemyFamily.Spirit, Style = AttackStyle.Magic, PowerScale = 0.7, HealthPool = 0.95, AddsAt = new[] { 0.66, 0.33 }, Line = "Shhhhh." }),
                Floor(15, "The Frozen Pond", "A pond frozen solid, and on it a lot of very small ducks with very sharp beaks.",
                    new[] { quackling, hare }, Defeat(quackling, 40), Defeat(hare, 40),
                    new TowerBoss { Name = "Emperor Quackers", Model = "ironhill-lord-quackers", Scale = 1.5f, Family = EnemyFamily.Beast, HealthPool = 1.1, SpitEvery = 4, Line = "QUACK." }),
                Floor(16, "Raiders' Mead Hall", "The raiders' long hall. The mead is warm, the welcome is not.",
                    new[] { raider, thane }, Defeat(raider, 50), Defeat(thane, 30),
                    new TowerBoss { Name = "Sigrid the Unthawed", Model = "iceheim-viking-chief", Scale = 2.5f, Family = EnemyFamily.Humanoid, PowerScale = 1.2, Line = "Mead first. Then I break you." }),
                Floor(17, "The Drake Roost", "Nests of ice on every ledge. The drakes do not share.",
                    new[] { drake, quackling }, Defeat(drake, 40), Defeat(quackling, 35),
                    new TowerBoss { Name = "Hoarfang", Model = "ironhill-dragon-white", Scale = 1.3f, Family = EnemyFamily.Dragon, Element = Element.Ice, ElementShare = 0.6, HealthPool = 1.1, AttackInterval = 3, SpitEvery = 3 }),
                Floor(18, "The Shiver Mines", "Old mines cut into the ice, and something frozen in the deepest shaft is waking up.",
                    new[] { ronin, frozenSkeleton }, Defeat(ronin, 40), Defeat(frozenSkeleton, 45),
                    new TowerBoss { Name = "The Thawed Ronin", Model = "kyojin-samurai", Scale = 2.3f, Family = EnemyFamily.Humanoid, PowerScale = 1.25, Line = "A thousand winters I waited for a worthy duel. You will have to do." }),
                Floor(19, "Cold Storage", "Somebody keeps their cows up here. To keep them fresh. They did not stay fresh.",
                    new[] { frostCow, drake }, Defeat(frostCow, 45), Defeat(drake, 40),
                    new TowerBoss { Name = "The Moo-tant, Refrozen", Model = "home-cow", Scale = 3.4f, Family = EnemyFamily.Beast, Element = Element.Ice, ElementShare = 0.6, SpitEvery = 4, Line = "Mooooo. Brrrr." }),
                Floor(20, "The Rime Throne", "At the top of the vault, on a throne of ice, the cold itself has a name.",
                    new[] { drake, thane }, Defeat(drake, 50), Defeat(thane, 45),
                    new TowerBoss { Name = "Glacius, the Winter Wyrm", Model = "ironhill-dragon-white", Scale = 1.7f, Family = EnemyFamily.Dragon, Element = Element.Ice, ElementShare = 0.7, HealthPool = 1.25, PowerScale = 1.2, AttackInterval = 3, SpitEvery = 3, AddsAt = new[] { 0.75, 0.5, 0.25 }, AddsCount = 4, EnrageSeconds = 420, Line = "Climb all you like. Up here, everything stops." }),
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

        private static string LayoutOf(int number)
        {
            switch (number)
            {
                case 3: case 8: return Marsh;
                case 4: case 7: case 9: case 10: return Crypt;
                case 11: case 12: case 15: case 16: case 19: return Glacier;
                case 13: case 14: case 17: case 18: case 20: return FrostVault;
                default: return Overgrown;
            }
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
                Layout = LayoutOf(number),
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
            if (name.EndsWith("s") || name.EndsWith("Ronin")) return name;
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
        /// <summary>Its size as a share of the look's (quacklings are a small Lord Quackers).</summary>
        public float Scale = 1f;
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
