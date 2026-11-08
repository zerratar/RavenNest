using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace RavenNest.Combat
{
    /// <summary>The seven gems (docs/gems-design.md in the game repo).</summary>
    public enum GemKind
    {
        Ruby,
        Sapphire,
        Topaz,
        Emerald,
        Diamond,
        Onyx,
        Amethyst,
    }

    /// <summary>A gem's tier: each one roughly doubles the bonus of the one before.</summary>
    public enum GemTier
    {
        Chipped,
        Flawed,
        Polished,
        Flawless,
        Perfect,
    }

    /// <summary>Where a gem sits decides what it does.</summary>
    public enum GemSlot
    {
        Weapon,
        Armor,
        Jewellery,
    }

    /// <summary>
    ///     Gems and sockets: an enchanted gem's bonus by kind, tier and where it sits, how many sockets an item has, the
    ///     tier odds when a clan enchants a raw gem, combining three into the next tier, and the chance to keep a gem
    ///     taken out. The same everywhere (server, game, website), so a gem always means the same.
    ///     An enchanted gem is an item with a fixed id (<see cref="ItemId"/>), so any reader knows it without a lookup.
    ///     An inventory item keeps its sockets as text: one gem item id per socket, empty for an empty one, joined by
    ///     commas (<see cref="ParseSockets"/>).
    /// </summary>
    public static class Gems
    {
        public const int KindCount = 7;
        public const int TierCount = 5;

        /// <summary>How much more each tier gives than Chipped.</summary>
        public static readonly double[] TierScale = { 1, 2, 3.5, 6, 10 };

        /// <summary>The coins to combine three gems of a tier into one of the next.</summary>
        public static readonly long[] CombineFee = { 1_000, 5_000, 25_000, 125_000 };

        /// <summary>Sockets come at these item levels (the highest skill level the item asks for).</summary>
        public const int FirstSocketLevel = 25;
        public const int SecondSocketLevel = 150;
        public const int ThirdSocketLevel = 400;

        /// <summary>No chance to keep a gem below this clan Enchanting level.</summary>
        public const int KeepFromClanLevel = 100;
        /// <summary>At this clan Enchanting level the clan's part of the keep chance is full.</summary>
        public const int KeepFullClanLevel = 400;
        public const double KeepClanMax = 0.40;
        public const double KeepPerSalvagerRank = 0.05;
        public const double KeepMax = 0.55;

        private static readonly Guid[,] ids = new Guid[KindCount, TierCount];
        private static readonly Dictionary<Guid, (GemKind Kind, GemTier Tier)> byId = new Dictionary<Guid, (GemKind, GemTier)>();

        static Gems()
        {
            for (var k = 0; k < KindCount; ++k)
            {
                for (var t = 0; t < TierCount; ++t)
                {
                    var id = NameGuid("ravenfall-gem-" + ((GemKind)k).ToString().ToLowerInvariant() + "-" + ((GemTier)t).ToString().ToLowerInvariant());
                    ids[k, t] = id;
                    byId[id] = ((GemKind)k, (GemTier)t);
                }
            }
        }

        // a fixed id from a name (MD5, version 3 style), so every reader agrees without a table to keep in step
        private static Guid NameGuid(string name)
        {
            byte[] hash;
            using (var md5 = MD5.Create()) hash = md5.ComputeHash(Encoding.UTF8.GetBytes(name));
            hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
            hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
            return new Guid(hash);
        }

        /// <summary>The item id of an enchanted gem.</summary>
        public static Guid ItemId(GemKind kind, GemTier tier) => ids[(int)kind, (int)tier];

        /// <summary>Is this item an enchanted gem, and which.</summary>
        public static bool TryGetGem(Guid itemId, out GemKind kind, out GemTier tier)
        {
            if (byId.TryGetValue(itemId, out var gem))
            {
                kind = gem.Kind;
                tier = gem.Tier;
                return true;
            }
            kind = default;
            tier = default;
            return false;
        }

        public static bool IsGem(Guid itemId) => byId.ContainsKey(itemId);

        /// <summary>The enchanted gem's item name: "Flawless Ruby".</summary>
        public static string Name(GemKind kind, GemTier tier) => tier + " " + kind;

        /// <summary>
        ///     The raw gem item each kind is enchanted from (its name in the item list): Ruby, Sapphire, Topaz and Emerald
        ///     are mined; Diamond, Onyx and Amethyst are new.
        /// </summary>
        public static string RawName(GemKind kind) => kind.ToString();

        public static bool TryParseKind(string text, out GemKind kind)
        {
            kind = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            foreach (GemKind k in Enum.GetValues(typeof(GemKind)))
            {
                if (k.ToString().StartsWith(text, StringComparison.OrdinalIgnoreCase)) { kind = k; return true; }
            }
            return false;
        }

        public static bool TryParseTier(string text, out GemTier tier)
        {
            tier = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            if (text.Equals("normal", StringComparison.OrdinalIgnoreCase)) { tier = GemTier.Polished; return true; }
            foreach (GemTier t in Enum.GetValues(typeof(GemTier)))
            {
                if (t.ToString().StartsWith(text, StringComparison.OrdinalIgnoreCase)) { tier = t; return true; }
            }
            return false;
        }

        /// <summary>
        ///     A gem's bonus where it sits. Element gems: damage in a weapon (the share of the weapon's hits that turns
        ///     to the element), resistance in armour, a stronger effect in jewellery. Diamond: crits. Onyx: life.
        ///     Amethyst: healing, every resistance a little, shorter cooldowns.
        /// </summary>
        public static GearStats Bonus(GemKind kind, GemTier tier, GemSlot slot)
        {
            var g = new GearStats();
            var s = TierScale[(int)tier];
            switch (kind)
            {
                case GemKind.Ruby: Element(g, Combat.Element.Fire, GearStat.BurnBonus, s, slot); break;
                case GemKind.Sapphire: Element(g, Combat.Element.Ice, GearStat.CrowdControlDuration, s, slot); break;
                case GemKind.Topaz: Element(g, Combat.Element.Lightning, GearStat.LightningJumpBonus, s, slot); break;
                case GemKind.Emerald: Element(g, Combat.Element.Poison, GearStat.PoisonBonus, s, slot); break;
                case GemKind.Diamond:
                    if (slot == GemSlot.Weapon) g[GearStat.CritChance] = 0.005 * s;
                    else if (slot == GemSlot.Armor) g[GearStat.CritResistance] = 0.01 * s;
                    else g[GearStat.CritDamage] = 0.02 * s;
                    break;
                case GemKind.Onyx:
                    if (slot == GemSlot.Weapon) g[GearStat.LifeSteal] = 0.004 * s;
                    else if (slot == GemSlot.Armor) g[GearStat.MaxHealthBonus] = 0.005 * s;
                    else g[GearStat.HealingReceived] = 0.02 * s;
                    break;
                case GemKind.Amethyst:
                    if (slot == GemSlot.Weapon) g[GearStat.HealingPowerBonus] = 0.02 * s;
                    else if (slot == GemSlot.Armor)
                    {
                        g[GearStat.FireResistance] = g[GearStat.IceResistance] = 0.005 * s;
                        g[GearStat.LightningResistance] = g[GearStat.PoisonResistance] = 0.005 * s;
                    }
                    else g[GearStat.CooldownReduction] = 0.015 * s;
                    break;
            }
            return g;
        }

        private static void Element(GearStats g, Element element, GearStat jewellery, double s, GemSlot slot)
        {
            if (slot == GemSlot.Weapon) g[GearStats.DamageOf(element)] = 0.03 * s;
            else if (slot == GemSlot.Armor) g[GearStats.ResistanceOf(element)] = 0.015 * s;
            else g[jewellery] = 0.04 * s;
        }

        /// <summary>An item's level for sockets (and enchanting): its skill requirements added up, or its own level when higher.</summary>
        public static int ItemLevel(int attack, int defense, int magic, int ranged, int slayer, int level)
        {
            var sum = attack + defense + magic + ranged + slayer;
            return sum > level ? sum : level;
        }

        /// <summary>How many sockets an item has: none below level 25, then 1, 2 from 150, 3 from 400; jewellery at most 1.</summary>
        public static int Sockets(int itemLevel, GemSlot slot)
        {
            var n = itemLevel >= ThirdSocketLevel ? 3 : itemLevel >= SecondSocketLevel ? 2 : itemLevel >= FirstSocketLevel ? 1 : 0;
            return slot == GemSlot.Jewellery ? Math.Min(1, n) : n;
        }

        /// <summary>The chance to get the gem back when taking it out (or socketing over it).</summary>
        public static double KeepChance(int clanEnchantingLevel, int salvagerRanks = 0)
        {
            var clan = 0.0;
            if (clanEnchantingLevel >= KeepFromClanLevel)
                clan = KeepClanMax * Math.Min(1.0, (clanEnchantingLevel - KeepFromClanLevel) / (double)(KeepFullClanLevel - KeepFromClanLevel));
            return Math.Min(KeepMax, clan + KeepPerSalvagerRank * Math.Max(0, Math.Min(3, salvagerRanks)));
        }

        /// <summary>The chances of each tier when a clan of this Enchanting level enchants a raw gem (Perfect only by combining).</summary>
        public static double[] EnchantOdds(int clanEnchantingLevel)
        {
            var l = clanEnchantingLevel;
            var flawless = Clamp((l - 250) / 1500.0, 0, 0.10);
            var polished = Clamp((l - 100) / 500.0, 0, 0.30);
            var flawed = Clamp((l - 30) / 200.0, 0, 0.50);
            return new[] { 1 - flawless - polished - flawed, flawed, polished, flawless, 0 };
        }

        /// <summary>The tier a roll in [0, 1) gives for a clan's Enchanting level.</summary>
        public static GemTier RollTier(int clanEnchantingLevel, double roll)
        {
            var odds = EnchantOdds(clanEnchantingLevel);
            // the best tiers first, so a low roll is lucky
            for (var t = TierCount - 1; t > 0; --t)
            {
                if (roll < odds[t]) return (GemTier)t;
                roll -= odds[t];
            }
            return GemTier.Chipped;
        }

        private static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;

        /// <summary>An item's sockets from its text: a gem item id per socket, null for an empty one.</summary>
        public static Guid?[] ParseSockets(string text, int count)
        {
            var sockets = new Guid?[Math.Max(0, count)];
            if (string.IsNullOrEmpty(text) || sockets.Length == 0) return sockets;
            var parts = text.Split(',');
            for (var i = 0; i < parts.Length && i < sockets.Length; ++i)
            {
                if (Guid.TryParse(parts[i].Trim(), out var id) && IsGem(id)) sockets[i] = id;
            }
            return sockets;
        }

        /// <summary>The text form of sockets; null when all are empty (so an item without gems stays as it was).</summary>
        public static string FormatSockets(IReadOnlyList<Guid?> sockets)
        {
            if (sockets == null) return null;
            var any = false;
            var sb = new StringBuilder();
            for (var i = 0; i < sockets.Count; ++i)
            {
                if (i > 0) sb.Append(',');
                if (sockets[i].HasValue) { sb.Append(sockets[i].Value.ToString()); any = true; }
            }
            if (!any) return null;
            // no trailing empty sockets
            return sb.ToString().TrimEnd(',');
        }

        /// <summary>Every gem's bonus in an item's sockets.</summary>
        public static GearStats SocketBonus(string text, GemSlot slot)
        {
            var total = new GearStats();
            if (string.IsNullOrEmpty(text)) return total;
            foreach (var part in text.Split(','))
            {
                if (Guid.TryParse(part.Trim(), out var id) && TryGetGem(id, out var kind, out var tier))
                    total.Add(Bonus(kind, tier, slot));
            }
            return total;
        }

        /// <summary>The bonus in a few words for tooltips and chat: "+18% Fire Damage".</summary>
        public static string Describe(GemKind kind, GemTier tier, GemSlot slot)
        {
            var g = Bonus(kind, tier, slot);
            if (kind == GemKind.Amethyst && slot == GemSlot.Armor)
                return "+" + Percent(g[GearStat.FireResistance]) + " to every resistance";
            for (var i = 0; i < GearStats.Count; ++i)
            {
                if (g.Values[i] != 0) return "+" + Percent(g.Values[i]) + " " + GearStats.Label((GearStat)i);
            }
            return "";
        }

        private static string Percent(double v) => (v * 100).ToString(v * 100 < 10 ? "0.#" : "0", System.Globalization.CultureInfo.InvariantCulture) + "%";
    }
}
