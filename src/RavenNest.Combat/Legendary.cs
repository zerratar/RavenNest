using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace RavenNest.Combat
{
    /// <summary>A Legendary's power (the game's docs/legendary-design.md, section 5).</summary>
    public enum LegendaryPower
    {
        /// <summary>Soulreaver: an enemy you struck that falls heals you.</summary>
        SoulHarvest,
        /// <summary>Hellhorn Mace: a hit may freeze the enemy in fear.</summary>
        Dread,
        /// <summary>Batwing Staff: now and then a spell sends bats that bite enemies near the target.</summary>
        BatSwarm,
        /// <summary>Spiked Bulwark: thorns against every blow, and a shield on you and the allies near you.</summary>
        IronAegis,
        /// <summary>Fortress Hand Cannon: every third shot is a blast that knocks the target back.</summary>
        ThunderShot,
    }

    /// <summary>Where a Legendary can be found (section 3).</summary>
    public enum LegendarySource
    {
        /// <summary>A tower boss of floor <see cref="Legendary.TowerFloor"/> and up, on the stream's clear.</summary>
        Tower,
        /// <summary>The boss of a Legendary (heroic) dungeon, tier 4.</summary>
        Dungeon,
        /// <summary>A raid boss.</summary>
        Raid,
    }

    /// <summary>A skill a Legendary asks for: to wear it (the item's requirement) or to find it.</summary>
    public enum LegendarySkill
    {
        Attack,
        Defense,
        Magic,
        Ranged,
        /// <summary>Only to find one: items have no Strength requirement (the Hellhorn Mace is worn with Attack).</summary>
        Strength,
    }

    /// <summary>One Legendary: which item, what it asks for and what it does.</summary>
    public sealed class LegendaryInfo
    {
        /// <summary>The Dark Fortress piece's key (its fixed item id is made from it).</summary>
        public string Key;
        public string Name;
        /// <summary>RavenNest.Models.ItemType: what it is compared with to stay the best.</summary>
        public int ItemType;
        public LegendarySkill Skill;
        /// <summary>The skill that must be 950 to find it, and whose tree's ending counts; null: <see cref="Skill"/>.</summary>
        public LegendarySkill? Finder;
        public LegendarySkill FindSkill => Finder ?? Skill;
        /// <summary>The combat tree whose ending (one of its three capstones) a character must have chosen.</summary>
        public SkillTree Tree;
        public LegendaryPower Power;
        public string PowerName;
        /// <summary>The glow's color, as #rrggbb.</summary>
        public string Color;
        public Guid ItemId;
        /// <summary>A shield: armor is what stays the best, not accuracy and power.</summary>
        public bool IsShield => ItemType == 13;
    }

    /// <summary>A power's numbers at a rank (section 5): only the ones the power uses are set.</summary>
    public struct LegendaryPowerValues
    {
        /// <summary>Soul Harvest: the share of maximum health a kill heals.</summary>
        public double KillHeal;
        /// <summary>Dread: the chance a hit freezes the enemy in fear, and for how long.</summary>
        public double FearChance, FearSeconds;
        /// <summary>Bat Swarm: how often (seconds), how many bites and each bite's share of the spell's damage.</summary>
        public double SwarmEvery, SwarmShare;
        public int SwarmBites;
        /// <summary>Iron Aegis: thorns added (they answer every style) and the shield's share of the wielder's health.</summary>
        public double Thorns, ShieldShare;
        /// <summary>Thunder Shot: the blast's extra damage, its splash share and the stagger in seconds.</summary>
        public double BlastBonus, BlastSplash, StaggerSeconds;
    }

    /// <summary>
    ///     The Legendary tier (the game's docs/legendary-design.md): which items are Legendary, who may find one and how
    ///     likely, the bad-luck count, awakening ranks, how a Legendary stays the best weapon of its type, and its power's
    ///     numbers. Shared by the server (which decides all of it), the game and the website (which show it) and the
    ///     simulator, so they never disagree.
    /// </summary>
    public static class Legendary
    {
        /// <summary>The level a Legendary asks for in its skill, and the level a character needs to find one.</summary>
        public const int RequiredLevel = 950;

        /// <summary>
        ///     The tower floor a character must have fought a clear of (the "climber" achievement): the first floor of
        ///     band 3, the Storm Spire, the top band on 2026-11-23. A constant, so a new band does not lock everyone out.
        /// </summary>
        public const int TowerFloor = 21;

        /// <summary>A find's base chance per qualifying boss kill.</summary>
        public const double TowerChance = 0.04;
        public const double DungeonChance = 0.0025;
        public const double RaidChance = 0.001;

        /// <summary>The dungeon tier whose boss counts (DungeonTier.Legendary; 5, Dynamic, counts too).</summary>
        public const int DungeonTier = 4;

        /// <summary>Bad-luck protection: the chance is base x min(<see cref="PityCap"/>, 1 + kills / <see cref="PityKills"/>).</summary>
        public const double PityKills = 75;
        public const double PityCap = 10;

        /// <summary>Boss kills with it worn to reach each rank (1 to 5): rank 1 when found.</summary>
        public static readonly int[] RankKills = { 0, 30, 90, 200, 400 };
        public const int MaxRank = 5;

        /// <summary>The margin over the best normal item of the type at rank 1, and what each rank adds.</summary>
        public const double BaseMargin = 0.10;
        public const double MarginPerRank = 0.025;

        /// <summary>The item's vendor price (it is never sold: the vendor refuses a Legendary).</summary>
        public const long SellPrice = 1;

        /// <summary>
        ///     Legend's Echo: a find for a character that already owns every Legendary it qualifies for adds this many boss
        ///     kills to its least awakened one instead; when all of those are at rank 5, a gem of <see cref="EchoGemTier"/>.
        /// </summary>
        public const int EchoKills = 10;
        public const GemTier EchoGemTier = GemTier.Flawless;

        /// <summary>The awakening count on the inventory item's tag: "legend:37".</summary>
        public const string TagPrefix = "legend:";

        public static readonly LegendaryInfo[] All =
        {
            new LegendaryInfo { Key = "soulreaver", Name = "Soulreaver", ItemType = 1, Skill = LegendarySkill.Attack, Tree = SkillTree.Precision,
                Power = LegendaryPower.SoulHarvest, PowerName = "Soul Harvest", Color = "#9fd8ff" },
            // worn with Attack like every one handed weapon, found by Strength and the Might ending (2026-11-23: so it is not
            // a second Precision find beside the Soulreaver)
            new LegendaryInfo { Key = "hellhorn-mace", Name = "Hellhorn Mace", ItemType = 4, Skill = LegendarySkill.Attack, Finder = LegendarySkill.Strength, Tree = SkillTree.Might,
                Power = LegendaryPower.Dread, PowerName = "Dread", Color = "#ff5a2a" },
            new LegendaryInfo { Key = "batwing-staff", Name = "Batwing Staff", ItemType = 5, Skill = LegendarySkill.Magic, Tree = SkillTree.Elements,
                Power = LegendaryPower.BatSwarm, PowerName = "Bat Swarm", Color = "#b06bff" },
            new LegendaryInfo { Key = "spiked-bulwark", Name = "Spiked Bulwark", ItemType = 13, Skill = LegendarySkill.Defense, Tree = SkillTree.Bulwark,
                Power = LegendaryPower.IronAegis, PowerName = "Iron Aegis", Color = "#7dffb0" },
            new LegendaryInfo { Key = "fortress-hand-cannon", Name = "Fortress Hand Cannon", ItemType = 6, Skill = LegendarySkill.Ranged, Tree = SkillTree.Marksman,
                Power = LegendaryPower.ThunderShot, PowerName = "Thunder Shot", Color = "#ffc247" },
        };

        private static readonly Dictionary<Guid, LegendaryInfo> byId = new Dictionary<Guid, LegendaryInfo>();

        static Legendary()
        {
            foreach (var l in All)
            {
                l.ItemId = ItemId(l.Key);
                byId[l.ItemId] = l;
            }
        }

        /// <summary>How many Legendaries there are (the "all of them" title).</summary>
        public static int Count => All.Length;

        /// <summary>The fixed item id of a Dark Fortress piece (the same as the server's GameData.DarkFortressItemId).</summary>
        public static Guid ItemId(string key)
        {
            byte[] hash;
            using (var md5 = MD5.Create()) hash = md5.ComputeHash(Encoding.UTF8.GetBytes("ravenfall-dark-fortress-" + key));
            hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
            hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
            return new Guid(hash);
        }

        public static bool IsLegendary(Guid itemId) => byId.ContainsKey(itemId);

        /// <summary>The Legendary of an item id, or null.</summary>
        public static LegendaryInfo Get(Guid itemId) => byId.TryGetValue(itemId, out var l) ? l : null;

        public static LegendaryInfo GetByKey(string key)
        {
            foreach (var l in All) if (string.Equals(l.Key, key, StringComparison.OrdinalIgnoreCase)) return l;
            return null;
        }

        // ---- finding one -------------------------------------------------------------------------------------------

        public static double BaseChance(LegendarySource source)
        {
            switch (source)
            {
                case LegendarySource.Tower: return TowerChance;
                case LegendarySource.Dungeon: return DungeonChance;
                default: return RaidChance;
            }
        }

        /// <summary>The chance of a find for a qualifying character with <paramref name="kills"/> qualifying kills since its last find.</summary>
        public static double Chance(LegendarySource source, int kills)
        {
            var pity = 1 + Math.Max(0, kills) / PityKills;
            return BaseChance(source) * Math.Min(PityCap, pity);
        }

        /// <summary>Does a roll in [0, 1) find one.</summary>
        public static bool Found(LegendarySource source, int kills, double roll) => roll < Chance(source, kills);

        /// <summary>
        ///     Does a character qualify for a Legendary (section 2): 950 in its skill, a clear of floor
        ///     <see cref="TowerFloor"/> or higher fought (the climber achievement's highest floor), the ending of its
        ///     tree chosen, and not owning it already.
        /// </summary>
        public static bool Qualifies(int skillLevel, int highestClearFought, bool endingChosen, bool owned) =>
            skillLevel >= RequiredLevel && highestClearFought >= TowerFloor && endingChosen && !owned;

        /// <summary>
        ///     Legend's Echo: of the owned Legendaries' boss kills, the one to awaken (the least awakened below rank 5,
        ///     the first on a tie), or -1 when every one is at rank 5 (then a gem).
        /// </summary>
        public static int EchoTarget(System.Collections.Generic.IReadOnlyList<int> kills)
        {
            var best = -1;
            if (kills == null) return best;
            for (var i = 0; i < kills.Count; ++i)
            {
                if (RankOf(kills[i]) >= MaxRank) continue;
                if (best < 0 || kills[i] < kills[best]) best = i;
            }
            return best;
        }

        /// <summary>Does a tower floor's boss count as a source.</summary>
        public static bool TowerCounts(int floor) => floor >= TowerFloor;

        /// <summary>Does a dungeon of this tier count as a source.</summary>
        public static bool DungeonCounts(int tier) => tier >= DungeonTier;

        // ---- awakening ------------------------------------------------------------------------------------------------

        /// <summary>The rank (1 to 5) for boss kills worn.</summary>
        public static int RankOf(int kills)
        {
            var rank = 1;
            for (var r = 1; r < RankKills.Length; ++r) if (kills >= RankKills[r]) rank = r + 1;
            return rank;
        }

        /// <summary>Boss kills worn to reach the next rank, or 0 at rank 5.</summary>
        public static int NextRankKills(int kills)
        {
            var rank = RankOf(kills);
            return rank >= MaxRank ? 0 : RankKills[rank];
        }

        /// <summary>The awakening count kept on the inventory item's tag; 0 for no tag (a fresh find).</summary>
        public static int KillsOf(string tag)
        {
            if (string.IsNullOrEmpty(tag) || !tag.StartsWith(TagPrefix, StringComparison.Ordinal)) return 0;
            return int.TryParse(tag.Substring(TagPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : 0;
        }

        public static string Tag(int kills) => TagPrefix + Math.Max(0, kills).ToString(CultureInfo.InvariantCulture);

        public static int RankOfTag(string tag) => RankOf(KillsOf(tag));

        /// <summary>The margin over the best normal item of the type at a rank.</summary>
        public static double Margin(int rank) => BaseMargin + MarginPerRank * (Clamp(rank) - 1);

        /// <summary>A rank's numbers as a multiple of the item row's (which holds rank 1).</summary>
        public static double Scale(int rank) => (1 + Margin(rank)) / (1 + Margin(1));

        /// <summary>A stat at a rank from the item row's rank 1 value.</summary>
        public static int AtRank(int rankOneValue, int rank) => rankOneValue <= 0 ? rankOneValue : (int)Math.Floor(rankOneValue * Scale(rank) + 0.5);

        /// <summary>The item row's value (rank 1) from the best normal item's.</summary>
        public static int Derive(int best) => best <= 0 ? 0 : (int)Math.Ceiling(best * (1 + Margin(1)) - 1e-9);

        /// <summary>Gem sockets at a rank: a normal item's three, a fourth at rank 3, a fifth at rank 5.</summary>
        public static int Sockets(int rank)
        {
            rank = Clamp(rank);
            return rank >= 5 ? 5 : rank >= 3 ? 4 : 3;
        }

        /// <summary>How strong the glow is at a rank (0: none, 1: full).</summary>
        public static float Glow(int rank)
        {
            rank = Clamp(rank);
            return rank >= 5 ? 1f : rank == 4 ? 0.65f : rank == 3 ? 0.35f : 0f;
        }

        private static int Clamp(int rank) => rank < 1 ? 1 : rank > MaxRank ? MaxRank : rank;

        // ---- powers ---------------------------------------------------------------------------------------------------

        private static double Lerp(double rankOne, double rankFive, int rank) => rankOne + (rankFive - rankOne) * (Clamp(rank) - 1) / (MaxRank - 1);

        /// <summary>A power's numbers at a rank (section 5 of the design; set with the simulator).</summary>
        public static LegendaryPowerValues Values(LegendaryPower power, int rank)
        {
            var v = new LegendaryPowerValues();
            switch (power)
            {
                case LegendaryPower.SoulHarvest:
                    v.KillHeal = Lerp(0.05, 0.10, rank);
                    break;
                case LegendaryPower.Dread:
                    v.FearChance = Lerp(0.08, 0.14, rank);
                    v.FearSeconds = Lerp(2, 3, rank);
                    break;
                case LegendaryPower.BatSwarm:
                    v.SwarmEvery = Lerp(10, 8, rank);
                    v.SwarmBites = 3;
                    v.SwarmShare = Lerp(0.35, 0.55, rank);
                    break;
                case LegendaryPower.IronAegis:
                    v.Thorns = Lerp(0.05, 0.12, rank);
                    v.ShieldShare = Lerp(0.05, 0.10, rank);
                    break;
                case LegendaryPower.ThunderShot:
                    v.BlastBonus = Lerp(0.25, 0.40, rank);
                    v.BlastSplash = Lerp(0.10, 0.20, rank);
                    v.StaggerSeconds = Lerp(0.8, 1.2, rank);
                    break;
            }
            return v;
        }

        /// <summary>The gear stats a power adds to the wielder's sheet (only Iron Aegis has any: its thorns).</summary>
        public static GearStats Stats(LegendaryPower power, int rank)
        {
            var s = new GearStats();
            if (power == LegendaryPower.IronAegis)
            {
                s[GearStat.Thorns] = Values(power, rank).Thorns;
                s[GearStat.ThornsAllStyles] = 1;
            }
            return s;
        }

        private static string Pct(double share) => Math.Round(share * 100, 1).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        private static string Sec(double s) => Math.Round(s, 1).ToString("0.#", CultureInfo.InvariantCulture) + " s";

        /// <summary>The power's text at a rank, for tooltips (English; the game words its own from the same numbers).</summary>
        public static string Describe(LegendaryPower power, int rank)
        {
            var v = Values(power, rank);
            switch (power)
            {
                case LegendaryPower.SoulHarvest:
                    return "Every enemy you struck that falls heals you for " + Pct(v.KillHeal) + " of your health.";
                case LegendaryPower.Dread:
                    return "A hit has a " + Pct(v.FearChance) + " chance to freeze the enemy in fear for " + Sec(v.FearSeconds) + ". Bosses shrug it off.";
                case LegendaryPower.BatSwarm:
                    return "Every " + Sec(v.SwarmEvery) + " a spell sends a swarm of bats: " + v.SwarmBites + " bites on enemies near the target for " + Pct(v.SwarmShare) + " of the spell's damage each.";
                case LegendaryPower.IronAegis:
                    return "+" + Pct(v.Thorns) + " thorns, and they answer ranged and magic blows too. Every " + Sec(LegendaryRules.AegisEvery) + " you and allies near you get a shield of " + Pct(v.ShieldShare) + " of your health.";
                case LegendaryPower.ThunderShot:
                    return "Every third shot is a blast: " + Pct(v.BlastBonus) + " more damage, " + Pct(v.BlastSplash) + " of it to up to two enemies around the target, and the target is knocked back and staggered for " + Sec(v.StaggerSeconds) + ".";
            }
            return "";
        }

        /// <summary>The awakening line for tooltips: "Rank 2 of 5: 37 of 90 boss kills to rank 3" or "Rank 5 of 5: fully awakened".</summary>
        public static string DescribeAwakening(int kills)
        {
            var rank = RankOf(kills);
            if (rank >= MaxRank) return "Rank 5 of 5: fully awakened";
            return "Rank " + rank + " of 5: " + kills.ToString(CultureInfo.InvariantCulture) + " of " + NextRankKills(kills).ToString(CultureInfo.InvariantCulture) + " boss kills to rank " + (rank + 1);
        }
    }

    /// <summary>What a Legendary's power remembers between blows (one per fighter): the next swarm and the shot count.</summary>
    public sealed class LegendaryState
    {
        /// <summary>Bat Swarm: when the next swarm is ready, on the caller's clock in seconds.</summary>
        public double SwarmReady;
        /// <summary>Thunder Shot: shots that hit since the last blast.</summary>
        public int Shots;
        /// <summary>Iron Aegis: when the next shield is ready.</summary>
        public double AegisReady;
    }

    /// <summary>
    ///     The powers' rules that need more than one blow (shared by the game and the simulator): the swarm's timer, the
    ///     third shot, the aegis timer, and what each one does with the numbers.
    /// </summary>
    public static class LegendaryRules
    {
        /// <summary>Bat Swarm bites enemies this near the target.</summary>
        public const double SwarmRadius = 8;
        /// <summary>Thunder Shot: every this many shots.</summary>
        public const int BlastEvery = 3;
        /// <summary>Thunder Shot's splash reaches this far around the target, and this many enemies at most.</summary>
        public const double BlastRadius = 3;
        public const int BlastTargets = 2;
        /// <summary>Thunder Shot pushes the target this far (bosses are not moved).</summary>
        public const double Knockback = 2.5;
        /// <summary>Iron Aegis: a shield this often while fighting, lasting this long, on allies this near.</summary>
        public const double AegisEvery = 10;
        public const double AegisSeconds = 6;
        public const double AegisRadius = 10;

        /// <summary>Dread: does a hit's roll in [0, 1) freeze the enemy (never a boss).</summary>
        public static bool Feared(LegendaryPowerValues v, bool boss, double roll) => !boss && v.FearChance > 0 && roll < v.FearChance;

        /// <summary>Soul Harvest: the health a kill gives back.</summary>
        public static int KillHeal(LegendaryPowerValues v, int maxHealth) => v.KillHeal <= 0 || maxHealth <= 0 ? 0 : Math.Max(1, (int)Math.Round(maxHealth * v.KillHeal));

        /// <summary>Bat Swarm: after a spell that hit for <paramref name="damage"/>, each bite's damage if a swarm goes now (then it waits), else 0.</summary>
        public static int Swarm(LegendaryState state, LegendaryPowerValues v, int damage, double now)
        {
            if (state == null || v.SwarmEvery <= 0 || damage <= 0 || now < state.SwarmReady) return 0;
            state.SwarmReady = now + v.SwarmEvery;
            return Math.Max(1, (int)Math.Round(damage * v.SwarmShare));
        }

        /// <summary>Thunder Shot: is the next shot that hits the third one (the blast).</summary>
        public static bool BlastNext(LegendaryState state) => state != null && state.Shots >= BlastEvery - 1;

        /// <summary>Thunder Shot: a shot hit; true when it was the blast (the count starts again).</summary>
        public static bool ShotHit(LegendaryState state)
        {
            if (state == null) return false;
            if (++state.Shots >= BlastEvery)
            {
                state.Shots = 0;
                return true;
            }
            return false;
        }

        /// <summary>Thunder Shot: the splash on enemies around the target from a blast of <paramref name="damage"/>.</summary>
        public static int BlastSplash(LegendaryPowerValues v, int damage) => damage <= 0 || v.BlastSplash <= 0 ? 0 : Math.Max(1, (int)Math.Round(damage * v.BlastSplash));

        /// <summary>Iron Aegis: true when the shield goes now (then it waits <see cref="AegisEvery"/>).</summary>
        public static bool Aegis(LegendaryState state, double now)
        {
            if (state == null || now < state.AegisReady) return false;
            state.AegisReady = now + AegisEvery;
            return true;
        }

        /// <summary>Iron Aegis: the shield each one near gets.</summary>
        public static int AegisShield(LegendaryPowerValues v, int wielderMaxHealth) => v.ShieldShare <= 0 || wielderMaxHealth <= 0 ? 0 : Math.Max(1, (int)Math.Round(wielderMaxHealth * v.ShieldShare));
    }
}
