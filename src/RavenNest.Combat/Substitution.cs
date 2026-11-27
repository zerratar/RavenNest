using System;
using System.Globalization;

namespace RavenNest.Combat
{
    /// <summary>How a Substitution looks when it saves its player; the look tells viewers which source it came from.</summary>
    public enum SubstitutionLook
    {
        None,
        /// <summary>The magical attribute: a puff of smoke, a log thuds down where the player stood, a second puff outside the ring.</summary>
        SmokeStep,
        /// <summary>The Shadowstone: the player turns into a shadow, slides out along the ground and takes shape at the ring's edge.</summary>
        ShadowPhase,
    }

    /// <summary>Where a Substitution find can come from: the rare drops of the climb, the Legendary dungeons and the raids.</summary>
    public enum SubstitutionSource
    {
        /// <summary>A tower part's chest from floor 2 up (its guardian fell on the stream's clear).</summary>
        TowerChest,
        /// <summary>A tower floor boss (the last part of a floor, floor 1's too).</summary>
        TowerFloorBoss,
        /// <summary>The boss of a Legendary dungeon (tier 4).</summary>
        DungeonBoss,
        /// <summary>A raid boss.</summary>
        RaidBoss,
    }

    /// <summary>
    ///     A player's Substitution: the chance to escape a boss's telegraphed area attack as it lands, the seconds before it
    ///     can happen again, and its look. Empty (<see cref="Has"/> false) for most players.
    /// </summary>
    [Serializable]
    public struct SubstitutionPower
    {
        public double Chance;
        public double Cooldown;
        public SubstitutionLook Look;

        public bool Has => Chance > 0 && Look != SubstitutionLook.None;

        public SubstitutionPower(double chance, double cooldown, SubstitutionLook look)
        {
            Chance = chance;
            Cooldown = cooldown;
            Look = look;
        }

        public override string ToString() => Has ? Substitution.Describe(this) : "none";
    }

    /// <summary>
    ///     Substitution (the owner's idea of 2026-11-27, the game's docs/substitution-escape-idea.md): a rare power that
    ///     lets a player escape a boss's telegraphed area attack (a tower guardian's slam or spit) as it lands, to a spot
    ///     just outside the ring, taking nothing from that blow. Never against a plain swing, so a tank is not made immune.
    ///     Two sources, each with its own look:
    ///     <list type="bullet">
    ///         <item>the magical attribute SUBSTITUTION on a piece of gear, the smoke step: 35 to 50% by the item's level,
    ///         every 30 s at most. Never rolled by enchanting; it only comes pre-rolled on rare drops;</item>
    ///         <item>the Shadowstone, a gem that is only found (never made or combined), in armor, the shadow phase: 40 to
    ///         60% and 30 to 18 s by its tier.</item>
    ///     </list>
    ///     With several sources the best chance and the shortest cooldown count; they never stack. The game rolls it (it
    ///     runs the fights), the server rolls the finds; the numbers are here so the simulator, the server and the game agree.
    /// </summary>
    public static class Substitution
    {
        /// <summary>The magical attribute's name in an item's enchantment text ("SUBSTITUTION:42%").</summary>
        public const string Key = "SUBSTITUTION";

        // ---- the magical attribute (the smoke step) ---------------------------------------------------------------

        /// <summary>The attribute's chance on an item of level <see cref="AttributeLowLevel"/> or less.</summary>
        public const double AttributeMinChance = 0.35;
        /// <summary>The attribute's chance on an item of level <see cref="AttributeHighLevel"/> or more.</summary>
        public const double AttributeMaxChance = 0.50;
        public const int AttributeLowLevel = 100;
        public const int AttributeHighLevel = 600;
        /// <summary>Seconds between two escapes with the attribute.</summary>
        public const double AttributeCooldown = 30;

        // ---- the Shadowstone (the shadow phase) --------------------------------------------------------------------

        /// <summary>The Shadowstone's chance by tier, Chipped to Perfect.</summary>
        public static readonly double[] GemChances = { 0.40, 0.45, 0.50, 0.55, 0.60 };
        /// <summary>The Shadowstone's cooldown in seconds by tier, Chipped to Perfect.</summary>
        public static readonly double[] GemCooldowns = { 30, 27, 24, 21, 18 };

        /// <summary>The most chance any source gives (a Perfect Shadowstone): an escape is never sure.</summary>
        public const double MaxChance = 0.60;
        /// <summary>The shortest cooldown any source gives (a Perfect Shadowstone).</summary>
        public const double MinCooldown = 18;

        /// <summary>
        ///     The attribute's chance on an item of this level (Gems.ItemLevel: its requirements added up): 35% to level 100,
        ///     rising to 50% at level 600, in whole percents.
        /// </summary>
        public static double AttributeChance(int itemLevel)
        {
            var k = (itemLevel - AttributeLowLevel) / (double)(AttributeHighLevel - AttributeLowLevel);
            k = k < 0 ? 0 : k > 1 ? 1 : k;
            return Math.Round((AttributeMinChance + (AttributeMaxChance - AttributeMinChance) * k) * 100) / 100.0;
        }

        /// <summary>The enchantment text of the attribute: "SUBSTITUTION:42%".</summary>
        public static string Enchantment(double chance) =>
            Key + ":" + Math.Round(chance * 100).ToString(CultureInfo.InvariantCulture) + "%";

        /// <summary>The power the attribute gives at this chance (capped at <see cref="AttributeMaxChance"/>).</summary>
        public static SubstitutionPower FromAttribute(double chance)
        {
            if (chance <= 0) return default;
            return new SubstitutionPower(Math.Min(AttributeMaxChance, chance), AttributeCooldown, SubstitutionLook.SmokeStep);
        }

        /// <summary>The power a Shadowstone of this tier gives in armor.</summary>
        public static SubstitutionPower FromGem(GemTier tier)
        {
            var t = Math.Max(0, Math.Min(Gems.TierCount - 1, (int)tier));
            return new SubstitutionPower(GemChances[t], GemCooldowns[t], SubstitutionLook.ShadowPhase);
        }

        /// <summary>
        ///     Two sources together: the best chance and the shortest cooldown, never added up. The look is the one of the
        ///     source with the better chance (on a tie the shorter cooldown's, then the shadow phase: the rarer find).
        /// </summary>
        public static SubstitutionPower Best(SubstitutionPower a, SubstitutionPower b)
        {
            if (!a.Has) return b.Has ? b : default;
            if (!b.Has) return a;
            SubstitutionLook look;
            if (a.Chance != b.Chance) look = a.Chance > b.Chance ? a.Look : b.Look;
            else if (a.Cooldown != b.Cooldown) look = a.Cooldown < b.Cooldown ? a.Look : b.Look;
            else look = a.Look == SubstitutionLook.ShadowPhase || b.Look == SubstitutionLook.ShadowPhase ? SubstitutionLook.ShadowPhase : a.Look;
            return new SubstitutionPower(Math.Min(MaxChance, Math.Max(a.Chance, b.Chance)), Math.Max(MinCooldown, Math.Min(a.Cooldown, b.Cooldown)), look);
        }

        /// <summary>The attribute in an item's enchantment text ("POWER:9%;SUBSTITUTION:42%"), or nothing.</summary>
        public static SubstitutionPower FromEnchantment(string enchantment)
        {
            if (string.IsNullOrEmpty(enchantment) || enchantment.IndexOf(Key, StringComparison.OrdinalIgnoreCase) < 0) return default;
            return FromAttribute(GearStats.Parse(enchantment)[GearStat.Substitution]);
        }

        /// <summary>The Shadowstones in an item's sockets (only armor makes them work), the best of them, or nothing.</summary>
        public static SubstitutionPower FromSockets(string sockets, GemSlot slot)
        {
            if (string.IsNullOrEmpty(sockets) || slot != GemSlot.Armor) return default;
            var best = default(SubstitutionPower);
            foreach (var part in sockets.Split(','))
            {
                if (Guid.TryParse(part.Trim(), out var id) && Gems.TryGetGem(id, out var kind, out var tier) && kind == GemKind.Shadowstone)
                    best = Best(best, FromGem(tier));
            }
            return best;
        }

        /// <summary>One worn item's Substitution: its attribute and its Shadowstones (null slot: an item that takes no gems).</summary>
        public static SubstitutionPower FromItem(string enchantment, string sockets, GemSlot? slot) =>
            Best(FromEnchantment(enchantment), slot.HasValue ? FromSockets(sockets, slot.Value) : default);

        /// <summary>Does the item carry the attribute (its enchantment text).</summary>
        public static bool HasAttribute(string enchantment) => FromEnchantment(enchantment).Has;

        /// <summary>
        ///     The enchantment text with the attribute taken out (enchanting replaces the rolled attributes but keeps it;
        ///     disenchanting takes the rest off): "POWER:9%;SUBSTITUTION:42%" gives "POWER:9%"; null when nothing is left.
        /// </summary>
        public static string WithoutAttribute(string enchantment, out string attribute)
        {
            attribute = null;
            if (string.IsNullOrEmpty(enchantment)) return null;
            var kept = new System.Collections.Generic.List<string>();
            foreach (var part in enchantment.Split(';'))
            {
                var entry = part.Trim();
                if (entry.Length == 0) continue;
                var colon = entry.IndexOf(':');
                var name = colon > 0 ? entry.Substring(0, colon).Trim() : entry;
                if (string.Equals(name, Key, StringComparison.OrdinalIgnoreCase)) attribute = entry;
                else kept.Add(entry);
            }
            return kept.Count == 0 ? null : string.Join(";", kept);
        }

        /// <summary>Joins an enchantment text and the attribute entry kept from before (either may be empty).</summary>
        public static string WithAttribute(string enchantment, string attribute)
        {
            if (string.IsNullOrEmpty(attribute)) return string.IsNullOrEmpty(enchantment) ? null : enchantment;
            return string.IsNullOrEmpty(enchantment) ? attribute : enchantment + ";" + attribute;
        }

        // ---- in a fight ----------------------------------------------------------------------------------------------

        /// <summary>Is the power ready: it has one and its cooldown since the last escape (seconds, negative for never) is over.</summary>
        public static bool Ready(SubstitutionPower power, double lastEscape, double now) =>
            power.Has && (lastEscape < 0 || now - lastEscape >= power.Cooldown);

        /// <summary>
        ///     A player stands inside a boss's area attack as it lands: does a roll in [0, 1) save them. Only ever asked for
        ///     telegraphed area attacks (slams and spits), never for a plain swing.
        /// </summary>
        public static bool Escapes(SubstitutionPower power, double lastEscape, double now, double roll) =>
            Ready(power, lastEscape, now) && roll < power.Chance;

        // ---- finding one -----------------------------------------------------------------------------------------------

        /// <summary>
        ///     The chance a character who fought gets a piece of gear with the attribute, by source. Tuned (with
        ///     <see cref="NightFinds"/>) so a regular stream sees one every three weeks or so, and the gem every two months.
        /// </summary>
        public static double AttributeChance(SubstitutionSource source)
        {
            switch (source)
            {
                case SubstitutionSource.TowerChest: return 0.003;
                case SubstitutionSource.TowerFloorBoss: return 0.02;
                case SubstitutionSource.DungeonBoss: return 0.003;
                default: return 0.0004;
            }
        }

        /// <summary>The chance a character who fought gets a Shadowstone, by source: about a third of the attribute's.</summary>
        public static double GemChance(SubstitutionSource source)
        {
            switch (source)
            {
                case SubstitutionSource.TowerChest: return 0.001;
                case SubstitutionSource.TowerFloorBoss: return 0.0075;
                case SubstitutionSource.DungeonBoss: return 0.001;
                default: return 0.00015;
            }
        }

        /// <summary>The tower part (as a step) whose clear rolls for a find, and as what: floor bosses, and every part from floor 2 up.</summary>
        public static SubstitutionSource? TowerSource(int step)
        {
            if (step <= 0) return null;
            if (Tower.IsFloorBoss(step)) return SubstitutionSource.TowerFloorBoss;
            return Tower.FloorOf(step) >= 2 ? SubstitutionSource.TowerChest : (SubstitutionSource?)null;
        }

        /// <summary>Does a dungeon of this tier count (the Legendary ones, tier 4).</summary>
        public static bool DungeonCounts(int tier) => tier >= 4;

        /// <summary>A found Shadowstone's tier odds, Chipped to Perfect.</summary>
        public static readonly double[] GemTierOdds = { 0.40, 0.30, 0.18, 0.09, 0.03 };

        /// <summary>A found Shadowstone's tier from a roll in [0, 1); a floor boss's is the better of two rolls.</summary>
        public static GemTier RollGemTier(double roll, double secondRoll = 1, bool better = false)
        {
            var tier = TierOf(roll);
            if (better) tier = (GemTier)Math.Max((int)tier, (int)TierOf(secondRoll));
            return tier;
        }

        private static GemTier TierOf(double roll)
        {
            // the best tiers first, so a low roll is lucky
            for (var t = Gems.TierCount - 1; t > 0; --t)
            {
                if (roll < GemTierOdds[t]) return (GemTier)t;
                roll -= GemTierOdds[t];
            }
            return GemTier.Chipped;
        }

        /// <summary>
        ///     The finds a stream can expect in one night (attribute and gem together, or one of them), for tuning: its raids
        ///     and the players in each, its Legendary dungeons, the tower parts it clears from floor 2 up and its floor
        ///     bosses, each with the players who fought.
        /// </summary>
        public static double NightFinds(double raids, double raidPlayers, double dungeons, double dungeonPlayers,
            double towerParts, double towerFloorBosses, double towerPlayers, bool attribute = true, bool gem = true)
        {
            double Chance(SubstitutionSource s) => (attribute ? AttributeChance(s) : 0) + (gem ? GemChance(s) : 0);
            return raids * raidPlayers * Chance(SubstitutionSource.RaidBoss)
                + dungeons * dungeonPlayers * Chance(SubstitutionSource.DungeonBoss)
                + towerParts * towerPlayers * Chance(SubstitutionSource.TowerChest)
                + towerFloorBosses * towerPlayers * Chance(SubstitutionSource.TowerFloorBoss);
        }

        // ---- words ------------------------------------------------------------------------------------------------------

        /// <summary>The look's name: "smoke step", "shadow phase".</summary>
        public static string LookName(SubstitutionLook look)
        {
            switch (look)
            {
                case SubstitutionLook.SmokeStep: return "smoke step";
                case SubstitutionLook.ShadowPhase: return "shadow phase";
                default: return "";
            }
        }

        /// <summary>The power in a few words for tooltips and chat: "45% chance to escape a boss's area attack, every 27 s at most".</summary>
        public static string Describe(SubstitutionPower power)
        {
            if (!power.Has) return "";
            return Percent(power.Chance) + " chance to escape a boss's area attack, every "
                + Math.Round(power.Cooldown).ToString(CultureInfo.InvariantCulture) + " s at most";
        }

        private static string Percent(double v) => Math.Round(v * 100).ToString(CultureInfo.InvariantCulture) + "%";
    }
}
