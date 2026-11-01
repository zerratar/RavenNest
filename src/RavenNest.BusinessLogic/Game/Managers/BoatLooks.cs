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

        /// <summary>The Rowboat and the Fishing Boat keep their models: a flag at the stern only.</summary>
        private static readonly Slot[] FlagOnly =
        {
            S("flag", 17, true, 0, 0, 1, 2, 3),
        };

        public static IReadOnlyList<Slot> SlotsFor(string boat) =>
            string.Equals(boat, "Sloop", StringComparison.OrdinalIgnoreCase) ? SmallShip : FlagOnly;

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
