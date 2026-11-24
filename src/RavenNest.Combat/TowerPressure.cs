using System;

namespace RavenNest.Combat
{
    /// <summary>
    ///     Scaling by pressure (the game's docs/tower-worlds-proposal.md, sections 11 and 12). Level sync already takes
    ///     away levels and gear above a part; what still flattens a part is how many players are in the fight and what
    ///     sync does not cap (skill trees, gems, enchantments). So a fight has a pressure: the players in it against the
    ///     group the part is made for (<see cref="Tower.SuggestedGroup"/>), times how much stronger than a fresh
    ///     level-synced player they are on average (<see cref="MinStrength"/> to <see cref="MaxStrength"/>).
    ///     Monsters take their health from it, guardians their health and helpers and a little of their blows. A full
    ///     group never meets less than a part's fixed numbers; smaller groups get mercy that shrinks with the floor
    ///     (<see cref="Mercy"/>), and how hard the scaling bites grows with the floor (<see cref="Bite"/>), so the first
    ///     floor stays friendly for any stream and the top is a real fight for everyone.
    ///     The game applies it (it runs the fights); the server works the same numbers out for what it records, and the
    ///     simulator's "tower" tunes them. One rule here so the three never disagree.
    /// </summary>
    public static class TowerPressure
    {
        /// <summary>A group's average strength counts between these: a fresh synced player is 1.</summary>
        public const double MinStrength = 1.0;
        public const double MaxStrength = 1.5;

        /// <summary>A guardian's blows grow by this share per point of pressure above 1, times the floor's bite, up to <see cref="MaxBlows"/>.</summary>
        public const double BlowsPerPressure = 0.05;
        public const double MaxBlows = 0.25;

        /// <summary>
        ///     How much harder than for a full group a guardian's fight is allowed to be for a small one: 1 for a full
        ///     group, and for each missing share of it the floor's bite more (two of five on floor 1: 1.24 times, on
        ///     floor 4: 1.6 times). A fight is as hard as the boss's blows times the time it takes, over the health
        ///     pools there are to wear down; <see cref="BossBlows"/> sets the blows to meet this.
        /// </summary>
        public static double SmallGroupHardness(int floor, double pressure) => 1 + Bite(floor) * (1 - CombatMath.Clamp(pressure, 0, 1));

        /// <summary>The most a monster's or guardian's health can be multiplied by, whatever the crowd.</summary>
        public const double MaxHealthScale = 12;

        /// <summary>Monsters come back this many times faster at most when a crowd is on the part.</summary>
        public const double MaxRespawnSpeed = 2.5;

        /// <summary>Every active skill in a player's loadout counts this much strength (the sheet does not show them).</summary>
        public const double StrengthPerActive = 0.04;

        /// <summary>
        ///     The pressure of a fight: 1 is the suggested group of fresh synced players. Two such players are 0.4,
        ///     thirty are 6, five veterans with trees and gems about 1.4.
        /// </summary>
        public static double Of(int players, double strength, int groupSize = Tower.SuggestedGroup) =>
            WithStrength(Math.Max(0, players) / (double)Math.Max(1, groupSize), CombatMath.Clamp(strength, MinStrength, MaxStrength));

        /// <summary>
        ///     Under this share of a full group a group's strength does not count at all, and from here it counts more
        ///     with every head, all of it from a full group on (<see cref="StrengthShare"/>).
        /// </summary>
        public const double StrengthFrom = 0.6;

        /// <summary>
        ///     How much of a group's strength counts as pressure, by its heads against the group the fight is made
        ///     for: nothing for a small group, all of it from a full group on. A small group's fight is made by the
        ///     small-group mercy (<see cref="BossBlows"/>, softer blows the fewer heads there are), and strength
        ///     counted as heads took that mercy away again: two veterans met blows twice as hard as two fresh players
        ///     and won far fewer fights, and five veterans met a harder floor boss than five fresh players (the
        ///     simulator, 2026-11-24). A group is never punished for its trees, gems and enchantments: below a full
        ///     group they are all its own, and from a full group on they mostly buy a longer fight (the guardian's
        ///     health follows the pressure, its blows hardly do).
        /// </summary>
        public static double StrengthShare(double headsPressure) =>
            CombatMath.Clamp((headsPressure - StrengthFrom) / (1 - StrengthFrom), 0, 1);

        private static double WithStrength(double headsPressure, double strength) =>
            headsPressure * (1 + (strength - 1) * StrengthShare(headsPressure));

        /// <summary>
        ///     A player far below a part (low levels, poor gear) counts as less than one player in the fight, down to
        ///     this share: a stream that brings its newcomers along does not meet a bigger fight for it.
        /// </summary>
        public const double MinHead = 0.25;

        /// <summary>
        ///     The pressure of a group from each player's strength (<see cref="Strength"/>): the players count as heads
        ///     against the suggested group, a player below a fresh synced one as that share of a head (at least
        ///     <see cref="MinHead"/>), and what the group has beyond fresh players, on average, multiplies it (each
        ///     player's between <see cref="MinStrength"/> and <see cref="MaxStrength"/>). Five fresh players are 1, as
        ///     with <see cref="Of(int, double, int)"/>.
        /// </summary>
        public static double Of(System.Collections.Generic.IReadOnlyList<double> strengths, int groupSize = Tower.SuggestedGroup)
        {
            if (strengths == null || strengths.Count == 0) return 0;
            var heads = 0.0;
            foreach (var s in strengths) heads += CombatMath.Clamp(s, MinHead, 1);
            return WithStrength(heads / Math.Max(1, groupSize), GroupStrength(strengths));
        }

        /// <summary>A group's average strength beyond fresh synced players: 1 for a group of them, up to <see cref="MaxStrength"/>.</summary>
        public static double GroupStrength(System.Collections.Generic.IReadOnlyList<double> strengths)
        {
            if (strengths == null || strengths.Count == 0) return MinStrength;
            var sum = 0.0;
            foreach (var s in strengths) sum += CombatMath.Clamp(s, MinStrength, MaxStrength);
            return sum / strengths.Count;
        }

        /// <summary>How much of the pressure above a full group a floor's fights follow: 40%, 60%, 80%, then all of it.</summary>
        public static double Bite(int floor) => floor <= 1 ? 0.4 : floor == 2 ? 0.6 : floor == 3 ? 0.8 : 1.0;

        /// <summary>
        ///     The least a floor's fights shrink to (their health) for a small group: half on floor 1, 55% on floor 2,
        ///     60% on floor 3, 70% from floor 4 on. The proposal's first numbers were 50, 65, 80 and 90%; with those a
        ///     group of two needed twice a full group's time on floor 3 and ran into the guardians' enrage, so the
        ///     simulator's numbers are gentler (a fight takes two players 1.25 to 1.75 times as long as five).
        /// </summary>
        public static double Mercy(int floor) => floor <= 1 ? 0.5 : floor == 2 ? 0.55 : floor == 3 ? 0.6 : 0.7;

        /// <summary>
        ///     What a fight's health is multiplied by on a floor: above a full group it follows the pressure by the
        ///     floor's bite (never below 1), under it it shrinks toward the group's size, down to the floor's mercy.
        /// </summary>
        public static double Health(int floor, double pressure)
        {
            if (pressure >= 1) return Math.Min(MaxHealthScale, 1 + Bite(floor) * (pressure - 1));
            return Math.Max(Mercy(floor), pressure);
        }

        /// <summary>A part's monsters: their health by pressure.</summary>
        public static double MonsterHealth(int floor, double pressure) => Health(floor, pressure);

        /// <summary>
        ///     A part's monsters come in numbers by pressure too: with a crowd on the part they come back sooner
        ///     (the spots are the layout's), never slower than the usual time.
        /// </summary>
        public static double RespawnSeconds(int floor, double pressure, double seconds = Tower.EnemyRespawnSeconds) =>
            seconds / CombatMath.Clamp(Math.Sqrt(Health(floor, pressure)), 1, MaxRespawnSpeed);

        /// <summary>A guardian's health: a mini boss's and a floor boss's alike.</summary>
        public static double BossHealth(int floor, double pressure) => Health(floor, pressure);

        /// <summary>
        ///     A guardian's blows: a little harder under a crowd (<see cref="BlowsPerPressure"/>). For a small group
        ///     they shrink more than its health does: with both halved, two players still lost every fight in the
        ///     simulator, because the one holding the boss takes every swing and there are two health pools to wear
        ///     down where a full group has five. So the blows are set from how hard the fight may be
        ///     (<see cref="SmallGroupHardness"/>).
        /// </summary>
        public static double BossBlows(int floor, double pressure)
        {
            if (pressure >= 1) return 1 + Bite(floor) * Math.Min(MaxBlows, BlowsPerPressure * (pressure - 1));
            // the fight takes Health / pressure as long and there are pressure times the health pools: for it to be
            // as hard as SmallGroupHardness says, the blows are that times pressure squared over the health
            var health = Health(floor, pressure);
            return Math.Min(1, SmallGroupHardness(floor, pressure) * pressure * pressure / Math.Max(0.01, health));
        }

        /// <summary>
        ///     How much harder a guardian strikes since knocked out players come back into the fight
        ///     (<see cref="TowerRecovery"/>, 2026-11-24). A fight is then only lost when nobody stands at the same
        ///     moment, and with the old blows a fresh group of five never lost one: the risk has to come from what
        ///     makes a group fall together, the guardian's blows and its slam (which is a share of a blow), not from
        ///     its health. Tuned in the simulator ("tower 200") for fresh synced players without healers or skills:
        ///     five win a mini boss about 9 times in 10 on floor 1, 8 in 10 on floor 2, 7 in 10 on floor 3, and a
        ///     floor boss 8 to 9 times in 10. Floor 2's guardians were the softest for their floor, so their number
        ///     is the biggest. From floor 4 on there are no guardians to measure yet: floor 3's number, and a tenth
        ///     more with every floor (to be measured when a floor is made).
        ///     A group smaller than the part's suggested one meets a share of it (<see cref="FuryShare"/>): two
        ///     players, who have two health pools to lose, get 40% of the extra.
        /// </summary>
        public static double Fury(int floor, bool floorBoss)
        {
            if (floorBoss) return floor <= 1 ? 1.5 : floor == 2 ? 1.65 : Math.Min(2.2, 1.45 + 0.1 * (floor - 3));
            return floor <= 1 ? 1.45 : floor == 2 ? 2.2 : Math.Min(2.6, 1.8 + 0.1 * (floor - 3));
        }

        /// <summary>
        ///     A floor boss's health against a full group, on top of its pool: its fight is clearly longer than a mini
        ///     boss's (about 4.5 minutes for five against 3 to 4). A smaller group meets a share of it, as of
        ///     <see cref="Fury"/>, so two players do not run into the time limit.
        /// </summary>
        public const double FloorBossPool = 1.8;

        /// <summary>
        ///     The share of <see cref="Fury"/> and <see cref="FloorBossPool"/> a group meets:
        ///     <paramref name="partPressure"/> is its pressure against the part's suggested group (not the floor
        ///     boss's bigger one), all of it from a full group on.
        /// </summary>
        public static double FuryShare(double partPressure) => CombatMath.Clamp(partPressure, 0, 1);

        /// <summary>
        ///     A guardian's helpers: half of the scaling goes to how many it calls and half to their health
        ///     (<see cref="HelperHealth"/>), so a crowd meets more of them and each lasts a little longer.
        /// </summary>
        public static int Helpers(int count, int floor, double pressure) =>
            count <= 0 ? 0 : Math.Max(1, (int)Math.Floor(count * Math.Sqrt(Health(floor, pressure)) + 0.5));

        public static double HelperHealth(int floor, double pressure) => Math.Sqrt(Health(floor, pressure));

        /// <summary>A health pool or a count scaled, at least 1.</summary>
        public static int Scale(int value, double scale) => Math.Max(1, (int)Math.Floor(value * scale + 0.5));

        // ---- Strength: how much more than a fresh synced player ---------------------------------------------------

        /// <summary>The mean of <see cref="CombatMath.RollHitShare"/>: a normal hit's share of the max hit.</summary>
        public const double MeanHitShare = 0.6695;

        /// <summary>
        ///     A fresh player synced to a step: every combat skill at the step's sync level, the best gear wearable at
        ///     it and nothing else (no tree, no gems, no enchantments). What strength is measured against.
        /// </summary>
        public static CombatSheet Fresh(int step)
        {
            var level = Tower.SyncLevel(step);
            var sheet = new CombatSheet { CombatLevel = level, DefenseLevel = level, IsPlayer = true };
            foreach (var s in new[] { sheet.Melee, sheet.Ranged, sheet.Magic, sheet.Healing })
            {
                s.DamageLevel = level;
                s.AimLevel = level;
                s.Power = 100000;
                s.Accuracy = 100000;
            }
            sheet.Armor = 100000;
            Tower.CapGear(sheet, level);
            sheet.MaxHealth = CombatMath.PlayerMaxHealth(level, CombatMath.ArmorHealth(sheet.Armor));
            return sheet;
        }

        /// <summary>
        ///     The damage a second a fighter's plain blows do to a target on average (no active skills): hit chance,
        ///     the mean hit, crits, elements against the target's resistances, armour and attack speed.
        /// </summary>
        public static double Offense(CombatSheet attacker, AttackStyle style, CombatSheet target)
        {
            if (attacker == null || target == null) return 0;
            if (style == AttackStyle.Healing) style = AttackStyle.Magic;
            var stats = attacker.Style(style);
            var hit = CombatMath.HitChance(CombatMath.AccuracyRating(stats.AimLevel, stats.EffectiveAccuracy), CombatMath.EvasionRating(target.DefenseLevel, target.GearEvasion));
            var raw = CombatMath.MaxHit(stats.DamageLevel, stats.EffectivePower) * MeanHitShare * attacker.DamageMultiplier;
            raw *= target.IsBoss ? 1 + attacker.BossDamage : 1 + attacker.NonBossDamage;
            if (target.Family == EnemyFamily.Undead) raw *= 1 + attacker.UndeadDamage;
            var crit = CombatMath.Clamp(attacker.CritChance - target.CritResistance, 0, 1);
            raw *= 1 + crit * (Math.Max(1, attacker.CritDamage) - 1);

            var converted = Math.Min(1, Math.Max(0, stats.Conversion.Total - stats.Conversion.Physical));
            var scale = converted > 0 ? converted / (stats.Conversion.Total - stats.Conversion.Physical) : 0;
            var reduction = CombatMath.ArmorReduction((int)(target.EffectiveArmor * (1 - CombatMath.Clamp(attacker.ArmorPenetration, 0, CombatMath.MaxArmorPenetration))));
            var total = 0.0;
            foreach (var element in Elements.All)
            {
                var share = element == Element.Physical ? 1 - converted : Math.Max(0, stats.Conversion[element]) * scale;
                share += attacker.ElementBonus[element];
                if (share <= 0) continue;
                var amount = raw * share * (element == Element.Physical ? 1 - reduction : 1 - reduction * CombatMath.ElementArmorShare);
                var resistance = target.Resistance[element] - (element == Element.Physical ? 0 : attacker.ResistanceShred);
                total += amount * CombatMath.ResistanceMultiplier(resistance, target.IsPlayer);
            }
            return total * hit * CombatMath.Clamp(attacker.AttackSpeed, 0.2, CombatMath.MaxAttackSpeed);
        }

        /// <summary>
        ///     How much a fighter can take: the health pool over what is left of a blow after evasion, armour, blocks
        ///     and damage taken (against an attacker of the target's making).
        /// </summary>
        public static double Toughness(CombatSheet defender, CombatSheet attacker)
        {
            if (defender == null || attacker == null) return 0;
            var hit = CombatMath.HitChance(CombatMath.AccuracyRating(attacker.Melee.AimLevel, attacker.Melee.EffectiveAccuracy), CombatMath.EvasionRating(defender.DefenseLevel, defender.GearEvasion));
            var left = 1 - CombatMath.ArmorReduction(defender.EffectiveArmor);
            left *= Math.Max(0.1, defender.DamageTakenMultiplier);
            left *= 1 - 0.5 * CombatMath.Clamp(defender.BlockChance, 0, CombatMath.MaxBlockChance);
            return defender.MaxHealth / Math.Max(0.01, hit * left);
        }

        /// <summary>
        ///     A player's strength on a step: 1 for a fresh synced player (<see cref="Fresh"/>), more for what sync does
        ///     not cap. Mostly what they deal (70%), partly what they take (30%), measured against the step's standard
        ///     monster, plus a little for every active skill in the loadout. <paramref name="synced"/> is the player's
        ///     sheet as it fights on the step (already synced). Not clamped: a weaker player is below 1, and a group's
        ///     average is clamped by <see cref="Of"/>.
        /// </summary>
        public static double Strength(CombatSheet synced, AttackStyle style, int step, int actives = 0)
        {
            var floor = Tower.Get(Math.Max(1, Math.Min(step, Tower.TopFloor)));
            if (synced == null || floor == null || floor.Enemies.Length == 0) return 1;
            var target = Tower.Enemy(floor, floor.Enemies[0]).Sheet();
            var fresh = Fresh(floor.Number);
            var dealt = Offense(fresh, style, target);
            var taken = Toughness(fresh, target);
            if (dealt <= 0 || taken <= 0) return 1;
            var offense = Offense(synced, style, target) / dealt;
            var toughness = Toughness(synced, target) / taken;
            if (offense <= 0 || toughness <= 0) return MinStrength;
            return Math.Pow(offense, 0.7) * Math.Pow(toughness, 0.3) + StrengthPerActive * Math.Max(0, actives);
        }
    }
}
