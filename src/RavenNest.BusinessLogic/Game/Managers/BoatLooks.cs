using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// What each boat's look is made of (the game's docs/own-boats.md, "Customizing boats"): the parts of Synty's Pirate
    /// Realm ships and how many of each there are, which ones every boat comes with, and the look's short recipe kept in
    /// the boat stack's Tag ("look:1;hull=2;sails=7;flag=3"). The game's BoatLook reads the same recipe.
    /// </summary>
    public static class BoatLooks
    {
        public const string Prefix = "look:1;";

        public sealed class Slot
        {
            public string Name;
            public int Count;
            /// <summary>0 means none (a ship without a figurehead or a flag).</summary>
            public bool CanBeNone;
            /// <summary>What every boat comes with, before anything is earned.</summary>
            public int[] Free;
            public int Default;
        }

        private static Slot S(string name, int count, bool none, int def, params int[] free) =>
            new Slot { Name = name, Count = count, CanBeNone = none, Default = def, Free = free };

        /// <summary>Pirate Realm's small ship (the Sloop): every part and look.</summary>
        private static readonly Slot[] SmallShip =
        {
            S("hull", 2, false, 1, 1),
            S("front", 4, false, 1, 1),
            S("middle", 3, false, 1, 1),
            S("back", 4, false, 3, 3),
            S("figurehead", 7, true, 0, 0),
            S("ram", 4, true, 0, 0),
            S("sails", 14, false, 1, 1, 2),
            S("flag", 17, true, 0, 0, 1, 2, 3),
            S("wood", 5, false, 1, 1, 2),
        };

        /// <summary>Pirate Realm's medium ship (the Brigantine): one hull (its rigging is made for it), the rest as the Sloop.</summary>
        private static readonly Slot[] MediumShip =
        {
            S("hull", 1, false, 1, 1),
            S("front", 3, false, 1, 1),
            S("middle", 4, false, 1, 1),
            S("back", 4, false, 1, 1),
            S("figurehead", 7, true, 0, 0),
            S("ram", 4, true, 0, 0),
            S("sails", 14, false, 1, 1, 2),
            S("flag", 17, true, 0, 0, 1, 2, 3),
            S("wood", 5, false, 1, 1, 2),
        };

        /// <summary>Pirate Realm's large ship (the Galleon).</summary>
        private static readonly Slot[] LargeShip =
        {
            S("hull", 1, false, 1, 1),
            S("front", 3, false, 1, 1),
            S("middle", 3, false, 1, 1),
            S("back", 3, false, 1, 1),
            S("figurehead", 7, true, 0, 0),
            S("ram", 4, true, 0, 0),
            S("sails", 14, false, 1, 1, 2),
            S("flag", 17, true, 0, 0, 1, 2, 3),
            S("wood", 5, false, 1, 1, 2),
        };

        /// <summary>The Rowboat and the Fishing Boat keep their models: a flag at the stern only.</summary>
        private static readonly Slot[] FlagOnly =
        {
            S("flag", 17, true, 0, 0, 1, 2, 3),
        };

        // ---- Earning (phase 2) ---------------------------------------------------------------------------------
        //
        // Nothing is bought (decided 2026-11-01). Some looks come with how far a player has come at sea, counted over
        // all their characters: these are owned while the condition holds, no row needed. The rest are found and won
        // (BoatLookUnlock rows): the regatta's sails, the treasure's flags and figureheads, the pirates' colours.

        public sealed class Earned
        {
            public string Key;
            /// <summary>The highest Sailing level among the player's characters, at least this.</summary>
            public int Sailing;
            /// <summary>Or an achievement's tier reached ("ferryman" 1 is the Ferryman, 2 the Sea Captain).</summary>
            public string Achievement;
            public int Tier;
            /// <summary>Or a quest chain finished ("shipwright").</summary>
            public string Chain;
            public string How;
        }

        private static Earned AtSailing(string key, int level) => new Earned { Key = key, Sailing = level, How = "Sailing " + level };

        public static readonly Earned[] ByProgress =
        {
            AtSailing("wood_3", 50),
            AtSailing("ram_1", 100), AtSailing("back_2", 100),
            AtSailing("front_2", 150),
            AtSailing("wood_4", 200), AtSailing("ram_2", 200), AtSailing("hull_2", 200),
            AtSailing("middle_2", 250), AtSailing("front_3", 250),
            AtSailing("wood_5", 300), AtSailing("ram_3", 300),
            AtSailing("middle_3", 400), AtSailing("front_4", 400), AtSailing("back_4", 400),
            AtSailing("ram_4", 500),
            new Earned { Key = "flag_13", Chain = "shipwright", How = "the shipwright's chain" },
            new Earned { Key = "figurehead_2", Achievement = "ferryman", Tier = 1, How = "the Ferryman" },
            new Earned { Key = "figurehead_6", Achievement = "ferryman", Tier = 2, How = "the Sea Captain" },
            new Earned { Key = "flag_10", Achievement = "ferryman", Tier = 2, How = "the Sea Captain" },
        };

        /// <summary>The regatta's sails: a winner gets one not yet owned (and any finisher, now and then).</summary>
        public static readonly int[] RegattaSails = { 3, 4, 5, 7, 8, 9, 10, 11, 12, 13, 14 };
        /// <summary>The treasure's flags and figureheads, found while digging.</summary>
        public static readonly int[] TreasureFlags = { 8, 9, 11, 12, 14, 15, 16, 17 };
        public static readonly int[] TreasureFigureheads = { 1, 3, 4, 5, 7 };
        /// <summary>The pirates' own colours, for holding their raid: the black sails and the skull and bones.</summary>
        public const string PirateSails = "sails_6";
        public const string PirateFlag = "flag_7";

        /// <summary>Where a look comes from, for the website's designer (the free ones are told apart by the caller).</summary>
        public static string HowToEarn(string slot, int variant)
        {
            var key = Key(slot, variant);
            var progress = ByProgress.FirstOrDefault(e => e.Key == key);
            if (progress != null)
                return progress.Sailing > 0 ? "Reach Sailing level " + progress.Sailing
                    : progress.Chain != null ? "Finish " + progress.How
                    : "Become " + progress.How;
            if (key == PirateSails || key == PirateFlag) return "Hold off the pirates' raid on the banana island";
            if (slot == "sails" && RegattaSails.Contains(variant)) return "Win the regatta at the banana island";
            if ((slot == "flag" && TreasureFlags.Contains(variant)) || (slot == "figurehead" && TreasureFigureheads.Contains(variant)))
                return "Dig for treasure on the banana island";
            return "Kept for events still to come";
        }

        public static IReadOnlyList<Slot> SlotsFor(string boat) =>
            string.Equals(boat, "Sloop", StringComparison.OrdinalIgnoreCase) ? SmallShip
            : string.Equals(boat, "Brigantine", StringComparison.OrdinalIgnoreCase) ? MediumShip
            : string.Equals(boat, "Galleon", StringComparison.OrdinalIgnoreCase) ? LargeShip
            : FlagOnly;

        public static Slot Find(string boat, string slot) =>
            SlotsFor(boat).FirstOrDefault(s => string.Equals(s.Name, slot, StringComparison.OrdinalIgnoreCase));

        public static string Key(string slot, int variant) => slot.ToLowerInvariant() + "_" + variant;

        /// <summary>The boat's look: its recipe over the boat's defaults.</summary>
        public static Dictionary<string, int> Parse(string boat, string tag)
        {
            var look = SlotsFor(boat).ToDictionary(s => s.Name, s => s.Default);
            if (string.IsNullOrEmpty(tag) || !tag.StartsWith("look:", StringComparison.Ordinal)) return look;
            foreach (var part in tag.Split(';'))
            {
                var kv = part.Split('=');
                if (kv.Length != 2 || !int.TryParse(kv[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) continue;
                var slot = Find(boat, kv[0]);
                if (slot != null && v >= (slot.CanBeNone ? 0 : 1) && v <= slot.Count) look[slot.Name] = v;
            }
            return look;
        }

        public static string Format(string boat, IReadOnlyDictionary<string, int> look) =>
            Prefix + string.Join(";", SlotsFor(boat).Select(s => s.Name + "=" + (look.TryGetValue(s.Name, out var v) ? v : s.Default).ToString(CultureInfo.InvariantCulture)));
    }
}
