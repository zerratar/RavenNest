using System;
using System.Collections.Concurrent;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    ///     Odd finds (v0.9.7, the game's docs/easter-eggs.md): now and then a gathering round turns up something silly
    ///     instead of nothing in particular: an Old Boot from the sea, Quackers the rubber duck off Kyo, a Pet Rock, an
    ///     empty Birds Nest, a Lookalike Potato or a Very Good Stick. Worth a coin at the vendor and good for a laugh
    ///     (the game has a line or two for !use and tells the stream). Only the rolls are here; ResourceTaskProcessor
    ///     gives the item and tells the game.
    /// </summary>
    /// <remarks>
    ///     Its own roll, apart from the drop table: it never takes the place of a real find, the Gathering tree and pets
    ///     do not change it, and the website's drop lists do not give it away. A round comes every 10 seconds, so
    ///     1 in 3,000 is about one odd find in 8 hours of gathering, and never two within 6 hours.
    /// </remarks>
    public static class OddFinds
    {
        public const string OldBoot = "Old Boot";
        public const string Quackers = "Quackers";
        public const string PetRock = "Pet Rock";
        public const string BirdsNest = "Birds Nest";
        public const string LookalikePotato = "Lookalike Potato";
        public const string VeryGoodStick = "Very Good Stick";

        public static readonly string[] Names = { OldBoot, Quackers, PetRock, BirdsNest, LookalikePotato, VeryGoodStick };

        /// <summary>The chance of an odd find in one gathering round.</summary>
        public const double ChancePerRound = 1.0 / 3000;

        /// <summary>The least time between two odd finds of one character.</summary>
        public static readonly TimeSpan Wait = TimeSpan.FromHours(6);

        /// <summary>The hidden achievement for odd finds: five of them make "the Hoarder".</summary>
        public const string Achievement = "oddities";

        /// <summary>Off Kyo, where the bottle says he was last seen, half the fishers' odd finds are Quackers.</summary>
        public const double QuackersOnKyo = 0.5;

        private static readonly ConcurrentDictionary<Guid, DateTime> lastFound = new ConcurrentDictionary<Guid, DateTime>();

        /// <summary>The odd thing a round of this skill finds on this island (Island.None at sea), from a roll (0 to 1).</summary>
        public static string Pick(Skill skill, Island island, double roll)
        {
            switch (skill)
            {
                case Skill.Fishing: return island == Island.Kyo && roll < QuackersOnKyo ? Quackers : OldBoot;
                case Skill.Mining: return PetRock;
                case Skill.Woodcutting: return BirdsNest;
                case Skill.Farming: return LookalikePotato;
                case Skill.Gathering: return VeryGoodStick;
                default: return null;
            }
        }

        /// <summary>Whether this round finds something odd, from a roll (0 to 1).</summary>
        public static bool Rolls(double roll) => roll < ChancePerRound;

        /// <summary>
        ///     Takes the character's turn: true (and the clock starts again) when the last odd find was at least
        ///     <see cref="Wait"/> ago, or there was none since the server started.
        /// </summary>
        public static bool TryTake(Guid characterId, DateTime now)
        {
            while (true)
            {
                if (!lastFound.TryGetValue(characterId, out var last))
                {
                    if (lastFound.TryAdd(characterId, now)) return true;
                    continue;
                }
                if (now - last < Wait) return false;
                if (lastFound.TryUpdate(characterId, now, last)) return true;
            }
        }

        /// <summary>For tests: forgets every character's last find.</summary>
        public static void Reset() => lastFound.Clear();
    }
}
