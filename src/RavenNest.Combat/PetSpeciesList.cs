using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Combat
{
    /// <summary>
    ///     The battle pet species (the game's docs/battle-pets-design.md, section 9): the creatures that can be tamed on
    ///     the islands, and a companion species for every pet item, so every pet a player has can fight. Each has a role,
    ///     an element and a strike style that fits its model: Bite for models with an attack clip, Pounce (a leap done in
    ///     code) for beasts without one, Bolt for small and magical pets that stay back.
    /// </summary>
    public static partial class BattlePets
    {
        private static List<PetSpecies> species;
        private static Dictionary<string, PetSpecies> speciesById;

        /// <summary>Every species: the tameable creatures first, then the companions.</summary>
        public static IReadOnlyList<PetSpecies> Species => species ?? (species = BuildSpecies());

        public static PetSpecies GetSpecies(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (speciesById == null) speciesById = Species.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
            return speciesById.TryGetValue(id.Trim(), out var s) ? s : Archetypes.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public static IEnumerable<PetSpecies> Tameable => Species.Where(s => s.Source == PetSource.Tamed);

        public static IEnumerable<PetSpecies> Companions => Species.Where(s => s.Source == PetSource.Companion);

        /// <summary>The species a creature tames into: by its definition id or its kind's name ("home-bunny", "Bunny", "Red Metalon").</summary>
        public static PetSpecies SpeciesForCreature(string definitionId, string kindName = null)
        {
            foreach (var s in Tameable)
            {
                foreach (var c in s.Creatures)
                {
                    if (Same(c, definitionId) || Same(c, kindName)) return s;
                }
            }
            return null;
        }

        /// <summary>The companion species a pet item wakes into, by its prefab ("Pets/Raven", "Pets/Halloween/Mummy").</summary>
        public static PetSpecies SpeciesForItem(string genericPrefab)
        {
            var key = PrefabKey(genericPrefab);
            if (key.Length == 0) return null;
            return Companions.FirstOrDefault(s => s.ItemPrefabs.Any(p => PrefabKey(p) == key));
        }

        private static bool Same(string a, string b) =>
            !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>"Pets/Halloween/Dark Mummy" and "DarkMummy" are the same key: the last part, without spaces, lower case.</summary>
        public static string PrefabKey(string prefab)
        {
            if (string.IsNullOrWhiteSpace(prefab)) return "";
            var name = prefab.Trim().Replace('\\', '/');
            var slash = name.LastIndexOf('/');
            if (slash >= 0) name = name.Substring(slash + 1);
            return name.Replace(" ", "").Replace("_", "").ToLowerInvariant();
        }

        /// <summary>A companion keeps its item's look; it grows a little with each stage instead.</summary>
        public static readonly double[] CompanionGrowth = { 1.0, 1.12, 1.25 };

        private static string[] RoleSkills(PetRole role, PetStrike strike, Element element)
        {
            switch (role)
            {
                case PetRole.Tank: return new[] { "guard", "harden", "bodyslam" };
                case PetRole.Healer: return new[] { "lick-wounds", "soothing-aura", "purify" };
                case PetRole.Support: return new[] { strike == PetStrike.Bolt ? "spark" : "howl", "hex", "lick-wounds" };
                case PetRole.Gatherer: return new[] { "forage", "keen-nose", "dig" };
                default:
                    var breath = element == Element.Ice ? "frost-breath" : element == Element.Poison ? "venom-breath" : element == Element.Lightning ? "storm-breath" : "fire-breath";
                    return new[] { strike == PetStrike.Bolt ? "barrage" : "pounce", "frenzy", breath };
            }
        }

        private static PetSpecies Make(string id, PetRole role, PetStrike strike, Element element, string[] names, Action<PetSpecies> more = null)
        {
            var stats = RoleStats(role);
            // bolt pets keep their distance, so they fight with magic's evasion and accuracy
            if (strike == PetStrike.Bolt) stats.Style = AttackStyle.Magic;
            var s = new PetSpecies
            {
                Id = id,
                Role = role,
                Strike = strike,
                Element = element,
                ElementShare = element == Element.Physical ? 0 : 0.5,
                Resistance = element == Element.Physical ? default : ElementValues.Of(element, 0.25),
                StageNames = names,
                Stats = stats,
                Skills = RoleSkills(role, strike, element),
            };
            more?.Invoke(s);
            return s;
        }

        private static PetSpecies Tamed(string id, PetRole role, PetStrike strike, Element element, string[] names, int tameLevel, string[] creatures, string[] models, double[] heights, EnemyFamily family, string blurb, Action<PetSpecies> more = null) =>
            Make(id, role, strike, element, names, s =>
            {
                s.Source = PetSource.Tamed;
                s.TameLevel = tameLevel;
                s.Creatures = creatures;
                s.StageModels = models;
                s.StageHeights = heights;
                s.Family = family;
                s.Blurb = blurb;
                s.Origin = "Tamed";
                more?.Invoke(s);
            });

        private static PetSpecies Companion(string id, PetRole role, PetStrike strike, Element element, string[] names, string[] prefabs, string blurb, Action<PetSpecies> more = null) =>
            Make(id, role, strike, element, names, s =>
            {
                s.Source = PetSource.Companion;
                s.ItemPrefabs = prefabs;
                s.StageModels = Array.Empty<string>(); // the item's own prefab
                s.StageHeights = Array.Empty<double>();
                s.Blurb = blurb;
                s.Origin = "A pet item, woken";
                more?.Invoke(s);
            });

        private static string[] N(params string[] names) => names;
        private static double[] H(params double[] heights) => heights;

        private static List<PetSpecies> BuildSpecies()
        {
            var list = new List<PetSpecies>
            {
                // ---------------------------------------------------------------- tameable creatures
                Tamed("bunny", PetRole.Support, PetStrike.Pounce, Element.Physical, N("Bunny", "Jackalope", "Moonhare"), 1,
                    new[] { "home-bunny", "Bunny" }, new[] { "enemy:Bunny" }, H(0.4, 0.5, 0.6), EnemyFamily.Beast,
                    "The first tame for most. Small, fluffy and surprisingly encouraging.",
                    s => s.Skills = new[] { "binky", "hex", "lick-wounds" }),
                Tamed("cow", PetRole.Tank, PetStrike.Pounce, Element.Physical, N("Calf", "Cow", "Moothunder"), 1,
                    new[] { "home-cow", "ravebridge-cow", "Cow" }, new[] { "enemy:Cow" }, H(0.8, 1.0, 1.25), EnemyFamily.Beast,
                    "Stands between you and danger and moos at it.",
                    s => s.Skills = new[] { "moo", "harden", "bodyslam" }),
                Tamed("tormented-soul", PetRole.Healer, PetStrike.Bolt, Element.Ice, N("Wisp", "Tormented Soul", "Wailing Wraith"), 15,
                    new[] { "ravebridge-tormented-soul", "Tormented Soul" }, new[] { "enemy:Tormented Soul" }, H(0.8, 1.0, 1.25), EnemyFamily.Spirit,
                    "A lost soul from Ravebridge that found someone to look after.",
                    s => s.Skills = new[] { "lick-wounds", "wail", "soothing-aura", "purify" }),
                Tamed("metalon", PetRole.Damage, PetStrike.Bite, Element.Poison, N("Red Metalon", "Purple Metalon", "Green Metalon"), 30,
                    new[] { "Red Metalon", "Purple Metalon", "Green Metalon", "home-metalon" },
                    new[] { "enemy:Red Metalon", "enemy:Purple Metalon", "enemy:Green Metalon" }, H(0.7, 0.85, 1.0), EnemyFamily.Insect,
                    "Changes colour as it grows: red, then purple, then green, the most venomous of all.",
                    s => s.Skills = new[] { "venom-sting", "frenzy", "venom-breath" }),
                Tamed("red-dragon", PetRole.Damage, PetStrike.Bite, Element.Fire, N("Red Whelp", "Red Drake", "Red Dragon"), 100,
                    new[] { "ironhill-dragon-red", "Red Dragon" }, new[] { "enemy:Red Dragon" }, H(0.5, 0.8, 1.2), EnemyFamily.Dragon,
                    "Hatched from Ironhill's fire. Breathes it too, once it is old enough.",
                    s => s.Skills = new[] { "pounce", "frenzy", "fire-breath" }),
                Tamed("blue-dragon", PetRole.Support, PetStrike.Bite, Element.Ice, N("Blue Whelp", "Blue Drake", "Blue Dragon"), 100,
                    new[] { "ironhill-dragon-blue", "Blue Dragon" }, new[] { "enemy:Blue Dragon" }, H(0.5, 0.8, 1.2), EnemyFamily.Dragon,
                    "Cold and calm; enemies near it feel the chill.",
                    s => s.Skills = new[] { "hex", "howl", "frost-breath" }),
                Tamed("white-dragon", PetRole.Tank, PetStrike.Bite, Element.Ice, N("White Whelp", "White Drake", "White Dragon"), 100,
                    new[] { "ironhill-dragon-white", "White Dragon" }, new[] { "enemy:White Dragon" }, H(0.5, 0.8, 1.2), EnemyFamily.Dragon,
                    "Scales like packed snow, and nothing gets past it.",
                    s => s.Skills = new[] { "guard", "harden", "frost-breath" }),
                Tamed("green-dragon", PetRole.Damage, PetStrike.Bite, Element.Poison, N("Green Whelp", "Green Drake", "Green Dragon"), 100,
                    new[] { "ironhill-dragon-green", "Green Dragon" }, new[] { "enemy:Green Dragon" }, H(0.5, 0.8, 1.2), EnemyFamily.Dragon,
                    "Its bite lingers.",
                    s => s.Skills = new[] { "venom-sting", "frenzy", "venom-breath" }),
                Tamed("purple-dragon", PetRole.Support, PetStrike.Bite, Element.Lightning, N("Purple Whelp", "Purple Drake", "Purple Dragon"), 100,
                    new[] { "ironhill-dragon-purple", "Purple Dragon" }, new[] { "enemy:Purple Dragon" }, H(0.5, 0.8, 1.2), EnemyFamily.Dragon,
                    "Crackles when it is happy, which is always.",
                    s => s.Skills = new[] { "spark", "howl", "storm-breath" }),
                Tamed("lord-quackers", PetRole.Damage, PetStrike.Bolt, Element.Fire, N("Duckling Quackers", "Sir Quackers", "Lord Quackers"), 150,
                    new[] { "ironhill-lord-quackers", "Lord Quackers" }, new[] { "enemy:Lord Quackers" }, H(0.45, 0.75, 1.1), EnemyFamily.Dragon,
                    "One of a kind. Very rare to tame, and very loud about it.",
                    s => { s.Skills = new[] { "quack", "barrage", "fire-breath" }; s.TameRarity = 0.1; }),

                // ---------------------------------------------------------------- companions: every pet item can fight
                Companion("companion-raven", PetRole.Support, PetStrike.Bolt, Element.Lightning, N("Raven", "Storm Raven", "Thunder Raven"),
                    new[] { "Pets/Raven", "Pets/BlackRaven", "Pets/SantaRaven", "Pets/BlackSantaRaven", "Pets/MagicSantaRaven" },
                    "Ravenfall's own: sparks at enemies from a safe height."),
                Companion("companion-diamond-raven", PetRole.Support, PetStrike.Bolt, Element.Ice, N("Diamond Raven", "Crystal Raven", "Prism Raven"),
                    new[] { "Pets/DiamondRaven" }, "Cold, sparkling and very pleased with itself."),
                Companion("companion-disco-raven", PetRole.Support, PetStrike.Bolt, Element.Lightning, N("Disco Raven", "Funky Raven", "Saturday Night Raven"),
                    new[] { "Pets/DiscoRaven" }, "Fights to a beat only it can hear.", s => s.Skills = new[] { "binky", "spark", "hex" }),
                Companion("companion-bacon-raven", PetRole.Damage, PetStrike.Bolt, Element.Fire, N("Bacon Raven", "Crispy Raven", "Extra Crispy Raven"),
                    new[] { "Pets/BaconRaven" }, "Smells wonderful. Burns things."),
                Companion("companion-turd-raven", PetRole.Damage, PetStrike.Bolt, Element.Poison, N("Turd Raven", "Stinky Raven", "Legendary Stinker"),
                    new[] { "Pets/TurdRaven" }, "Nobody asked how it fights. Nobody wants to know."),
                Companion("companion-cat", PetRole.Damage, PetStrike.Pounce, Element.Physical, N("Kitty", "Housecat Supreme", "Lord of the Sofa"),
                    new[] { "Pets/Pspsps Cat", "Pets/Pspsps Cat Beige", "Pets/Pspsps Cat Black White", "Pets/Pspsps Cat Black", "Pets/Pspsps Cat Cream",
                            "Pets/Pspsps Cat Mi Ke", "Pets/Pspsps Cat Orange", "Pets/Pspsps Cat Raccoon", "Pets/Pspsps Cat Siamese", "Pets/Pspsps Cat White",
                            "Pets/Rajah", "Pets/Halloween/Rajah" },
                    "Pspsps. It comes when it wants to, and it pounces when it wants to."),
                Companion("companion-raccoon", PetRole.Gatherer, PetStrike.Pounce, Element.Physical, N("Raccoon", "Trash Bandit", "Trash Panda King"),
                    new[] { "Pets/Raccoon", "Pets/Raccoon Brown", "Pets/Raccoon Baby", "Pets/Christmas/Santa Raccoon" },
                    "Finds things. Some of them were even lost."),
                Companion("companion-magic-raccoon", PetRole.Support, PetStrike.Bolt, Element.Lightning, N("Magic Raccoon", "Arcane Bandit", "Archmage of the Bins"),
                    new[] { "Pets/Raccoon Magic" }, "Learned magic from a book it stole."),
                Companion("companion-bear", PetRole.Tank, PetStrike.Pounce, Element.Physical, N("Bear", "Grizzly", "Ursa Major"),
                    new[] { "Pets/Bear_HD" }, "Big, warm and in the way of anything that wants to hurt you."),
                Companion("companion-polar-bear", PetRole.Tank, PetStrike.Pounce, Element.Ice, N("Polar Bear", "Frost Bear", "Glacier"),
                    new[] { "Pets/PolarBear_HD" }, "Brings its own weather."),
                Companion("companion-wolf", PetRole.Damage, PetStrike.Pounce, Element.Physical, N("Wolf", "Dire Wolf", "Alpha"),
                    new[] { "Pets/Wolf_HD" }, "Hunts beside you.", s => s.Skills = new[] { "pounce", "howl", "frenzy" }),
                Companion("companion-fox", PetRole.Damage, PetStrike.Pounce, Element.Fire, N("Fox", "Ember Fox", "Ninetail"),
                    new[] { "Pets/Fox_HD" }, "Quick, clever and a little bit on fire."),
                Companion("companion-deer", PetRole.Healer, PetStrike.Bolt, Element.Physical, N("Deer", "Stag", "Forest Spirit"),
                    new[] { "Pets/Deer_HD" }, "Gentle; the forest's calm comes with it."),
                Companion("companion-red-panda", PetRole.Support, PetStrike.Pounce, Element.Fire, N("Red Panda", "Firefox", "Blaze Panda"),
                    new[] { "Pets/RedPanda_HD" }, "Cheers you on with tiny furious noises."),
                Companion("companion-bat", PetRole.Damage, PetStrike.Bite, Element.Physical, N("Bat", "Vampire Bat", "Night Terror"),
                    new[] { "Pets/Bat" }, "Bites, flaps, bites again."),
                Companion("companion-ghost", PetRole.Support, PetStrike.Bite, Element.Ice, N("Ghost", "Spook", "Poltergeist"),
                    new[] { "Pets/Ghost" }, "Boo.", s => s.Skills = new[] { "wail", "hex", "spark" }),
                Companion("companion-pumpkin", PetRole.Tank, PetStrike.Bite, Element.Fire, N("Pumpkin", "Jack", "Pumpking"),
                    new[] { "Pets/Pumpkin" }, "Hollow inside, which is why the hits do not hurt."),
                Companion("companion-spider", PetRole.Damage, PetStrike.Bite, Element.Poison, N("Spider", "Widow", "Broodmother"),
                    new[] { "Pets/Spider" }, "Eight legs, all of them busy.", s => s.Skills = new[] { "venom-sting", "frenzy", "venom-breath" }),
                Companion("companion-werewolf", PetRole.Damage, PetStrike.Bite, Element.Physical, N("Werewolf", "Moonfang", "Lord of the Hunt"),
                    new[] { "Pets/Werewolf" }, "A good boy, mostly."),
                Companion("companion-yeti", PetRole.Tank, PetStrike.Bite, Element.Ice, N("Yeti", "Abominable", "Mountain King"),
                    new[] { "Pets/Yeti" }, "Hugs too hard."),
                Companion("companion-octopus", PetRole.Healer, PetStrike.Bolt, Element.Ice, N("Octopus", "Tidecaller", "Kraken"),
                    new[] { "Pets/Octopus", "Pets/Christmas/Santa Octopus" }, "Eight arms to hug you better with."),
                Companion("companion-blue-orb", PetRole.Support, PetStrike.Bolt, Element.Ice, N("Blue Orb", "Frost Orb", "Glacial Orb"),
                    new[] { "Pets/Blue_ORB" }, "Hums quietly."),
                Companion("companion-green-orb", PetRole.Healer, PetStrike.Bolt, Element.Poison, N("Green Orb", "Life Orb", "Verdant Orb"),
                    new[] { "Pets/Green_ORB" }, "Glows brighter when you are hurt."),
                Companion("companion-red-orb", PetRole.Damage, PetStrike.Bolt, Element.Fire, N("Red Orb", "Ember Orb", "Inferno Orb"),
                    new[] { "Pets/Red_ORB" }, "Warm to the touch. Do not touch it."),
                Companion("companion-monkey", PetRole.Damage, PetStrike.Bolt, Element.Physical, N("Monkey", "Banana Bandit", "Silverback"),
                    new[] { "Pets/MonkeyPet" }, "Came from Mangrove with a pocket full of bananas.", s => s.Skills = new[] { "banana-toss", "frenzy", "barrage" }),
                Companion("companion-santa-metalon", PetRole.Tank, PetStrike.Pounce, Element.Ice, N("Santa Metalon", "Jolly Metalon", "Metalon Claus"),
                    new[] { "Pets/RedSantaMetalon", "Pets/GreenSantaMetalon", "Pets/PurpleSantaMetalon" }, "Ho ho ho. Clack clack."),
                Companion("companion-mummy", PetRole.Tank, PetStrike.Pounce, Element.Physical, N("Mummy", "Pharaoh", "Eternal King"),
                    new[] { "Pets/Halloween/Mummy", "Pets/Halloween/Dark Mummy" }, "Wrapped up and ready to take the hits."),
                Companion("companion-ghoul", PetRole.Damage, PetStrike.Pounce, Element.Poison, N("Ghoul", "Ghast", "Grave Lord"),
                    new[] { "Pets/Halloween/Ghoul" }, "Hungry."),
                Companion("companion-goatman", PetRole.Damage, PetStrike.Pounce, Element.Fire, N("Goatman", "Satyr", "Horned One"),
                    new[] { "Pets/Halloween/Goatman" }, "Headbutts first, asks never."),
                Companion("companion-reaper", PetRole.Damage, PetStrike.Pounce, Element.Ice, N("Reaper", "Soul Reaper", "Grim"),
                    new[] { "Pets/Halloween/Reaper" }, "Small scythe, big ambitions."),
                Companion("companion-skeleton", PetRole.Damage, PetStrike.Pounce, Element.Physical, N("Skeleton", "Bone Knight", "Bone Lord"),
                    new[] { "Pets/Halloween/Skeleton" }, "Rattles into battle."),
                Companion("companion-dark-witch", PetRole.Healer, PetStrike.Bolt, Element.Poison, N("Dark Witch", "Hexer", "Coven Mother"),
                    new[] { "Pets/Halloween/Dark Witch" }, "Her potions mostly help."),
                Companion("companion-purple-witch", PetRole.Support, PetStrike.Bolt, Element.Lightning, N("Purple Witch", "Stormcaller", "Sky Witch"),
                    new[] { "Pets/Halloween/Purple Witch" }, "Calls down little bolts of mischief."),
                Companion("companion-red-witch", PetRole.Damage, PetStrike.Bolt, Element.Fire, N("Red Witch", "Pyromancer", "Flame Witch"),
                    new[] { "Pets/Halloween/Red Witch" }, "Everything is better on fire, she says."),
                Companion("companion-crachy", PetRole.Gatherer, PetStrike.Pounce, Element.Physical, N("Crachy", "Big Crachy", "Crachy the Great"),
                    new[] { "Pets/Crachy" }, "Nobody knows what Crachy is. Crachy does not know either."),
            };
            return list;
        }
    }

    /// <summary>
    ///     Taming (docs/battle-pets-design.md, section 2): !tame a kind of creature and each kill of one is a try. The
    ///     chance comes from the Taming level against the species' level; every try gives Taming experience, so a high
    ///     level player trains Taming by trying rather than starting from nothing.
    /// </summary>
    public static class Taming
    {
        public const double BaseChance = 0.02;
        public const double ChancePerLevelAbove = 0.0004;
        public const double MaxChance = 0.08;
        public const double TreatMultiplier = 1.5;

        /// <summary>The chance that a try tames the creature (0 when the Taming level is too low).</summary>
        public static double Chance(int tamingLevel, PetSpecies species, bool treat = false)
        {
            if (species == null || tamingLevel < species.TameLevel) return 0;
            var c = Math.Min(MaxChance, BaseChance + (tamingLevel - species.TameLevel) * ChancePerLevelAbove) * Math.Max(0, species.TameRarity);
            return treat ? c * TreatMultiplier : c;
        }

        /// <summary>
        ///     How many tries at a creature of its own level a Taming level takes: few early, more later. The game turns it
        ///     into experience per try (the next level's experience divided by this).
        /// </summary>
        public static double TriesPerLevel(int tamingLevel) => 8 + Math.Max(1, tamingLevel) * 0.25;

        /// <summary>A try at a creature far below the Taming level still counts, for half.</summary>
        public static double TryShare(int tamingLevel, int tameLevel) =>
            Math.Min(1, 0.5 + 0.5 * Math.Max(1, tameLevel) / Math.Max(1, tamingLevel));

        /// <summary>What is wrong with a try, or null: no such creature, or Taming too low.</summary>
        public static string CannotTame(int tamingLevel, PetSpecies species, string creature)
        {
            if (species == null) return (string.IsNullOrWhiteSpace(creature) ? "That" : creature) + " cannot be tamed.";
            if (tamingLevel < species.TameLevel) return species.NameAt(0) + " needs Taming " + species.TameLevel + ".";
            return null;
        }
    }
}
