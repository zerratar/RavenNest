using System;
using System.Collections.Generic;

namespace RavenNest.Combat
{
    /// <summary>
    ///     Class gear in a tower clear's chest (the game's docs/class-gear-design.md, "From the tower"): beside the
    ///     chest's other items a player can get a piece of class gear made for the floor, with a gem of the band's
    ///     element already in its first socket. The rules that are only numbers are here, shared and tested; which
    ///     line fits a player and which pieces a line has is the server's (TowerManager, GameData.ClassGear).
    /// </summary>
    public static class TowerLoot
    {
        /// <summary>
        ///     The first time a stream clears a floor, everyone who fought gets a piece. Today that is every clear that
        ///     pays: a stream clears each floor once, and bosses on floors below it are training.
        /// </summary>
        public const double FirstClearChance = 1.0;

        /// <summary>A clear of a floor the stream had cleared before, should those ever pay: one in ten.</summary>
        public const double RepeatClearChance = 0.10;

        /// <summary>The chance that one player who fought gets a class gear piece from a clear.</summary>
        public static double GearChance(bool firstClearByStream) => firstClearByStream ? FirstClearChance : RepeatClearChance;

        /// <summary>Does a roll in [0, 1) give the piece.</summary>
        public static bool Drops(bool firstClearByStream, double roll) => roll < GearChance(firstClearByStream);

        /// <summary>
        ///     A fighter with a battle pet out gets a Beastmaster piece (the Taming line, the game's
        ///     docs/taming-gear-design.md) in place of their skill's line this often: one piece in five. Over the 30
        ///     floors of the climb that is about six, a set's worth, while most of what they get stays their skill's.
        /// </summary>
        public const double BeastmasterChance = 0.2;

        /// <summary>Does a roll in [0, 1) make a player's piece a Beastmaster one (only with a pet out).</summary>
        public static bool Beastmaster(bool petOut, double roll) => petOut && roll < BeastmasterChance;

        /// <summary>
        ///     The tier of the piece, 1 for the first: the highest whose level (<paramref name="tierLevels"/>, the
        ///     class gear tiers' levels, lowest first) is at or below the floor's level (<see cref="Tower.Level"/>).
        ///     The server then lowers it to what the character can wear.
        /// </summary>
        public static int GearTier(IReadOnlyList<int> tierLevels, int floor)
        {
            var level = Tower.Level(floor);
            var tier = 1;
            if (tierLevels == null) return tier;
            for (var i = 0; i < tierLevels.Count; ++i)
            {
                if (tierLevels[i] <= level) tier = i + 1;
            }
            return tier;
        }

        /// <summary>The gem of an element (Ruby fire, Sapphire ice, Topaz lightning, Emerald poison); null for physical.</summary>
        public static GemKind? ElementGem(Element element)
        {
            switch (element)
            {
                case Element.Fire: return GemKind.Ruby;
                case Element.Ice: return GemKind.Sapphire;
                case Element.Lightning: return GemKind.Topaz;
                case Element.Poison: return GemKind.Emerald;
                default: return null;
            }
        }

        /// <summary>
        ///     The gem of a line's role, for a band without an element, by how much of plate's armor the line has
        ///     (so a line added later needs no entry): heavy lines (nine tenths and up) an Onyx (health), cloth (four
        ///     tenths and less) an Amethyst (every resistance a little), the ones between a Diamond (crits against
        ///     you land less often).
        /// </summary>
        public static GemKind RoleGem(double armorFactor)
        {
            if (armorFactor >= 0.9) return GemKind.Onyx;
            if (armorFactor <= 0.4) return GemKind.Amethyst;
            return GemKind.Diamond;
        }

        /// <summary>The gem a floor's piece comes with: the band's element's, or the line's role gem where the band has none.</summary>
        public static GemKind Gem(int floor, double armorFactor)
        {
            var band = Tower.BandOf(floor);
            var element = band != null ? ElementGem(band.Element) : null;
            return element ?? RoleGem(armorFactor);
        }

        /// <summary>
        ///     The gem's tier grows with the floor, along the tower's milestone floors: Chipped to floor 10, Flawed to
        ///     25, Polished to 50, Flawless to 75, Perfect above. A band's last floor (every tenth) gives one better.
        /// </summary>
        public static GemTier GemTier(int floor)
        {
            var tier = floor <= 10 ? 0 : floor <= 25 ? 1 : floor <= 50 ? 2 : floor <= 75 ? 3 : 4;
            if (floor > 0 && floor % 10 == 0) tier++;
            return (GemTier)Math.Min(Gems.TierCount - 1, tier);
        }

        /// <summary>
        ///     The piece's sockets text with the gem in the first socket (<see cref="Gems.ParseSockets"/> reads it), or
        ///     null for a piece without sockets (below item level 25: tier 1).
        /// </summary>
        public static string Sockets(GemKind kind, GemTier tier, int socketCount)
        {
            if (socketCount <= 0) return null;
            var sockets = new Guid?[socketCount];
            sockets[0] = Gems.ItemId(kind, tier);
            return Gems.FormatSockets(sockets);
        }

        /// <summary>One of <paramref name="count"/> things from a roll in [0, 1): 0 to count - 1.</summary>
        public static int Pick(int count, double roll)
        {
            if (count <= 1) return 0;
            var index = (int)(roll * count);
            return index < 0 ? 0 : index >= count ? count - 1 : index;
        }
    }
}
