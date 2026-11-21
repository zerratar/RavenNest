using System.Linq;
using Xunit;

namespace RavenNest.Combat.Tests
{
    public class BattlePetsTests
    {
        [Fact]
        public void Every_species_is_whole()
        {
            var ids = BattlePets.Species.Select(s => s.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
            foreach (var s in BattlePets.Species)
            {
                Assert.Equal(3, s.StageNames.Length);
                Assert.All(s.StageNames, n => Assert.False(string.IsNullOrWhiteSpace(n), s.Id));
                Assert.NotEmpty(s.Skills);
                Assert.All(s.Skills, k => Assert.True(BattlePets.GetSkill(k) != null, s.Id + ": " + k));
                // gatherers help while gathering; the others need a skill that fights at level 1
                if (s.Role != PetRole.Gatherer) Assert.Contains(s.SkillsAt(1, 0), k => k.IsActive);
                if (s.Source == PetSource.Tamed)
                {
                    Assert.NotEmpty(s.Creatures);
                    Assert.NotEmpty(s.Treats);
                    Assert.NotEmpty(s.StageModels);
                    Assert.Equal(3, s.StageHeights.Length);
                }
                else if (s.Source == PetSource.Companion)
                {
                    Assert.NotEmpty(s.ItemPrefabs);
                    Assert.Equal(0, s.HeightAt(0));
                }
                else
                {
                    Assert.NotEmpty(s.StageModels);
                    Assert.True(s.HeightAt(0) > 0, s.Id);
                }
                Assert.True(s.Sheet(50, 1).MaxHealth > 0);
            }
        }

        [Fact]
        public void Creatures_and_items_find_their_species()
        {
            Assert.Equal("bunny", BattlePets.SpeciesForCreature("home-bunny")?.Id);
            Assert.Equal("cow", BattlePets.SpeciesForCreature(null, "Cow")?.Id);
            Assert.Equal("metalon", BattlePets.SpeciesForCreature("", "Purple Metalon")?.Id);
            Assert.Equal("red-dragon", BattlePets.SpeciesForCreature("ironhill-dragon-red", "Red Dragon")?.Id);
            Assert.Null(BattlePets.SpeciesForCreature("mangrove-pirate", "Pirate")); // never people
            Assert.Equal("companion-raven", BattlePets.SpeciesForItem("Pets/Raven")?.Id);
            Assert.Equal("companion-mummy", BattlePets.SpeciesForItem("Pets/Halloween/Dark Mummy")?.Id);
            Assert.Equal("companion-mummy", BattlePets.SpeciesForItem("DarkMummy")?.Id);
            Assert.Equal("companion-cat", BattlePets.SpeciesForItem("Pets/Pspsps Cat Mi Ke")?.Id);
            Assert.Null(BattlePets.SpeciesForItem("Pets/NoSuchPet"));
            Assert.Equal("starter-fox", BattlePets.FindStarter("fox")?.Id);
            Assert.Equal("starter-bear", BattlePets.FindStarter("tank")?.Id);
            Assert.Equal("starter-fawn", BattlePets.FindStarter("Fawn")?.Id);
            Assert.Equal(3, BattlePets.Starters.Count());
        }

        [Fact]
        public void Taming_odds()
        {
            var bunny = BattlePets.GetSpecies("bunny");
            var dragon = BattlePets.GetSpecies("red-dragon");
            var quackers = BattlePets.GetSpecies("lord-quackers");
            Assert.Equal(0.02, Taming.Chance(1, bunny), 6);
            Assert.Equal(0.08, Taming.Chance(999, bunny), 6);
            Assert.Equal(0.12, Taming.Chance(999, bunny, treat: true), 6);
            Assert.Equal(0, Taming.Chance(99, dragon));
            Assert.Equal(0.02, Taming.Chance(100, dragon), 6);
            Assert.True(Taming.Chance(999, quackers) < 0.01);
            // the creature's name, not its baby form's (players looked for whelps), and what to do about it
            Assert.StartsWith("Taming a Red Dragon needs Taming 100 (yours is 50).", Taming.CannotTame(50, dragon, "Red Dragon"));
            Assert.Equal("Red Dragon", dragon.CreatureName);
            Assert.Equal("Bunny", bunny.CreatureName);
            Assert.Equal("Cow", BattlePets.GetSpecies("cow").CreatureName);
            Assert.Equal("Metalon", BattlePets.GetSpecies("metalon").CreatureName);
            Assert.Equal("Lord Quackers", quackers.CreatureName);
            Assert.Equal("Pirate cannot be tamed.", Taming.CannotTame(50, null, "Pirate"));
            Assert.Null(Taming.CannotTame(100, dragon, "Red Dragon"));
            Assert.Equal(1, Taming.TryShare(100, 100), 6);
            Assert.True(Taming.TryShare(600, 1) >= 0.5);
        }

        [Fact]
        public void Rookery_is_modest()
        {
            Assert.Equal(0, Rookery.ExperienceBonus(0));
            Assert.Equal(0.05, Rookery.ExperienceBonus(100), 6);
            Assert.Equal(0.5, Rookery.ExperienceBonus(999), 2); // 49.95%
            Assert.Equal(0, Rookery.ExtraStablePlaces(99));
            Assert.Equal(1, Rookery.ExtraStablePlaces(100));
            Assert.Equal(2, Rookery.ExtraStablePlaces(999));
        }

        /// <summary>Days of play (minutes a day with at least one member's pet out, members out at once) to reach a Rookery level.</summary>
        private static double DaysTo(int target, double minutesADay, int membersOut)
        {
            var minutes = 0d;
            for (var level = 1; level < target; ++level)
                minutes += 1 / (Rookery.ShareOfLevel(level, membersOut, 1) * membersOut); // the clan's minutes for this level
            return minutes / minutesADay;
        }

        [Fact]
        public void Rookery_takes_a_big_clan_a_year_and_a_half()
        {
            // a really big clan: 100 members with a pet out at once, around the clock, reaches the top in about 1.5 years
            Assert.InRange(DaysTo(999, 24 * 60, 100), 480, 600);
            // a big one: 20 members out 16 hours a day
            var big = DaysTo(500, 16 * 60, 20);
            Assert.InRange(big, 250, 350);
            Assert.True(DaysTo(999, 16 * 60, 20) > 3 * 365);
            // a small clan: 3 members, 4 hours a day
            Assert.InRange(DaysTo(100, 4 * 60, 3), 60, 130);
            // hundreds of members out at once barely beat dozens
            Assert.True(DaysTo(500, 16 * 60, 300) > 0.5 * big);
            // the first levels come in an evening
            Assert.True(DaysTo(5, 4 * 60, 3) < 1);
        }

        [Fact]
        public void Quests_and_events_give_eggs_that_fit()
        {
            // every new source has eggs, and the events' eggs belong there
            foreach (var source in new[] { EggSource.Quest, EggSource.PirateRaid, EggSource.Regatta, EggSource.Ravebridge })
                Assert.NotEmpty(PetEggs.Table(source, 0));
            Assert.All(PetEggs.Table(EggSource.Regatta, 0), t => Assert.Contains(t.Species.Id, new[] { "shell", "sun-blossom", "capybara", "penguin", "parrot" }));
            Assert.Equal("shell", PetEggs.Table(EggSource.Regatta, 0).OrderByDescending(t => t.Weight).First().Species.Id);
            Assert.Equal("bomb", PetEggs.Table(EggSource.PirateRaid, 0).OrderByDescending(t => t.Weight).First().Species.Id);
            Assert.Equal("practice-dummy", PetEggs.Table(EggSource.Ravebridge, 0).OrderByDescending(t => t.Weight).First().Species.Id);
            // quests can give any egg, the dragon least often
            Assert.Equal(PetEggs.Species.Count(), PetEggs.Table(EggSource.Quest, 0).Count());
            Assert.Equal("dragon-spark", PetEggs.Table(EggSource.Quest, 0).OrderBy(t => t.Weight).First().Species.Id);
            // days played in a row: the first egg on day 7, then every 14 days
            Assert.False(PetEggs.StreakGivesEgg(6));
            Assert.True(PetEggs.StreakGivesEgg(7));
            Assert.False(PetEggs.StreakGivesEgg(14));
            Assert.True(PetEggs.StreakGivesEgg(21));
            Assert.False(PetEggs.StreakGivesEgg(28));
            Assert.True(PetEggs.StreakGivesEgg(35));
            Assert.False(PetEggs.StreakGivesEgg(0));
            Assert.NotNull(BattlePets.GetSpecies(PetEggs.SpeckledChainEgg));
        }

        [Fact]
        public void Eggs_hatch_every_cute_chain()
        {
            var eggs = PetEggs.Species.ToList();
            var cute = eggs.Where(s => s.StageModels.All(m => m.StartsWith("pet:Pets/Cute/"))).ToList();
            Assert.Equal(8, cute.Count);
            // every egg is a cute series chain or one of the animals
            Assert.Equal(eggs.Count, cute.Count + BattlePets.Animals.Count());
            // all 24 cute series models, each used once, in three stages per chain
            var models = cute.SelectMany(s => s.StageModels).ToList();
            Assert.Equal(24, models.Distinct().Count());
            Assert.All(cute, s => Assert.Equal(3, s.StageModels.Length));
            Assert.Equal("pet:Pets/Cute/Werewolf", BattlePets.GetSpecies("wolf-pup").ModelAt(2));
            // egg items: fixed, distinct, and found again from the id or a name
            var ids = eggs.Select(s => PetEggs.ItemId(s.Id)).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.All(eggs, s => Assert.Same(s, PetEggs.SpeciesForEgg(PetEggs.ItemId(s.Id))));
            Assert.Null(PetEggs.SpeciesForEgg(System.Guid.NewGuid()));
            Assert.Equal("wolf-pup", PetEggs.FindEgg("wolf pup egg").Id);
            Assert.Equal("dragon-spark", PetEggs.FindEgg("dragon").Id);
            Assert.Equal("bud", PetEggs.FindEgg("Bud Seed").Id);
            // every egg can be found somewhere, and every source has eggs from the first tier
            foreach (var s in eggs) Assert.Contains(System.Enum.GetValues(typeof(EggSource)).Cast<EggSource>(), src => PetEggs.Table(src, 4).Any(t => t.Species == s));
            foreach (EggSource src in System.Enum.GetValues(typeof(EggSource))) Assert.NotEmpty(PetEggs.Table(src, 0));
            // the dragon is rare, and only in the hardest dungeons
            Assert.DoesNotContain(PetEggs.Table(EggSource.Dungeon, 2), t => t.Species.Id == "dragon-spark");
            var raid = PetEggs.Table(EggSource.Raid, 0).ToList();
            Assert.True(raid.First(t => t.Species.Id == "dragon-spark").Weight / raid.Sum(t => t.Weight) < 0.05);
            // a roll covers the whole table
            Assert.NotNull(PetEggs.Pick(EggSource.Tower, 0, 0));
            Assert.NotNull(PetEggs.Pick(EggSource.Tower, 0, 1));
        }

        [Fact]
        public void Animals_widen_the_roster()
        {
            var animals = BattlePets.Animals.ToList();
            Assert.Equal(18, animals.Count);
            var others = BattlePets.Species.Except(animals).ToList();
            var taken = others.SelectMany(s => s.StageNames).ToList();
            foreach (var s in animals)
            {
                // an egg pet like the cute series, with what the website and the chat answers need
                Assert.True(s.Hatches, s.Id);
                Assert.Equal(PetSource.Egg, s.Source);
                Assert.True(s.HatchMinutes >= 30 && s.HatchMinutes <= 240, s.Id);
                Assert.True(s.TameLevel >= 1 && s.TameLevel <= 100, s.Id);
                Assert.False(string.IsNullOrWhiteSpace(s.Blurb), s.Id);
                Assert.False(string.IsNullOrWhiteSpace(s.EggFrom), s.Id);
                Assert.EndsWith(" Egg", s.EggName);
                // one model that grows, or three that follow each other; under Resources/Pets/Animals, no spaces
                Assert.True(s.StageModels.Length == 1 || s.StageModels.Length == 3, s.Id);
                Assert.All(s.StageModels, m => Assert.StartsWith(BattlePets.AnimalModels, m));
                Assert.All(s.StageModels, m => Assert.DoesNotContain(" ", m));
                Assert.Equal(3, s.StageHeights.Length);
                Assert.True(s.HeightAt(0) < s.HeightAt(1) && s.HeightAt(1) < s.HeightAt(2), s.Id);
                // names of its own, short enough for a pet's name, and "a Panda Cub", "a Panda Egg" read right in chat
                Assert.Equal(3, s.StageNames.Distinct().Count());
                Assert.All(s.StageNames, n => Assert.DoesNotContain(n, taken));
                Assert.All(s.StageNames, n => Assert.True(n.Length <= BattlePets.MaxNameLength, n));
                Assert.DoesNotContain(char.ToLowerInvariant(s.NameAt(0)[0]), "aeiou");
                Assert.DoesNotContain(char.ToLowerInvariant(s.EggName[0]), "aeiou");
                // found by its id, its egg and its names
                Assert.Same(s, PetEggs.FindEgg(s.Id));
                Assert.Same(s, PetEggs.FindEgg(s.EggName));
                Assert.Same(s, PetEggs.FindEgg(s.NameAt(0)));
                // in a quest's eggs, and in at least one other place
                Assert.Contains(PetEggs.Table(EggSource.Quest, 0), t => t.Species == s);
                Assert.Contains(new[] { EggSource.Dungeon, EggSource.Raid, EggSource.Tower, EggSource.PirateRaid, EggSource.Regatta, EggSource.Ravebridge },
                    src => PetEggs.Table(src, 4).Any(t => t.Species == s));
                // a skill of its own from level 1, and more to learn later
                Assert.True(s.Skills.Length >= 3, s.Id);
                Assert.Equal(s.Skills.Length, s.Skills.Distinct().Count());
                Assert.Contains(s.Skills, k => BattlePets.GetSkill(k).LearnLevel > 1);
            }
            Assert.Equal(animals.Count, animals.Select(s => s.EggName).Distinct().Count());
            Assert.DoesNotContain(animals, s => others.Any(o => o.EggName == s.EggName));

            // every role and every element is there
            foreach (PetRole role in System.Enum.GetValues(typeof(PetRole))) Assert.True(animals.Count(s => s.Role == role) >= 3, role.ToString());
            foreach (var element in new[] { Element.Fire, Element.Ice, Element.Lightning, Element.Poison }) Assert.True(animals.Count(s => s.Element == element) >= 2, element.ToString());
            // a bolt pet fights with magic from afar; the others up close
            Assert.All(animals.Where(s => s.Strike == PetStrike.Bolt), s => Assert.Equal(AttackStyle.Magic, s.Stats.Style));

            // color variants and kinds that follow each other
            Assert.Equal("pet:Pets/Animals/Horse_Black", BattlePets.GetSpecies("horse").ModelAt(2));
            Assert.Equal("pet:Pets/Animals/Dog_Chihuahua", BattlePets.GetSpecies("dog").ModelAt(0));
            Assert.Equal("pet:Pets/Animals/Hen", BattlePets.GetSpecies("chicken").ModelAt(1));
            Assert.Equal("pet:Pets/Animals/Lion_Male", BattlePets.GetSpecies("lion").ModelAt(2));
            Assert.Equal("pet:Pets/Animals/Panda", BattlePets.GetSpecies("panda").ModelAt(2));
            // the same kinds as pet items stay their own species
            Assert.Equal("companion-cat", BattlePets.SpeciesForItem("Pets/Pspsps Cat")?.Id);
            Assert.Equal("companion-spider", BattlePets.SpeciesForItem("Pets/Spider")?.Id);

            // their own skills: a heal for itself, a shield, a freeze, a root and a weakened target, in line with the rest
            var snack = BattlePets.GetSkill("bamboo-snack").Active;
            Assert.Equal(SkillUse.SelfHurt, snack.Use);
            Assert.Equal(SkillEffectKind.HealPercent, snack.Effects[0].Kind);
            Assert.InRange(snack.Effects[0].Amount, 0.1, 0.25);
            Assert.Equal(SkillEffectKind.Shield, BattlePets.GetSkill("wool-coat").Active.Effects[0].Kind);
            Assert.Contains(BattlePets.GetSkill("snowball").Active.Effects, e => e.Kind == SkillEffectKind.Freeze && e.Seconds <= 2);
            Assert.Contains(BattlePets.GetSkill("web").Active.Effects, e => e.Kind == SkillEffectKind.Root && e.Seconds <= 3);
            Assert.Contains(BattlePets.GetSkill("honk").Active.Effects, e => e.Kind == SkillEffectKind.DamageDealt && e.Amount < 0);
            Assert.Same(BattlePets.GetSkill("honk"), BattlePets.FindSkill("HONK"));
            Assert.Contains("bamboo-snack", BattlePets.GetSpecies("panda").Skills);
            Assert.DoesNotContain(BattlePets.GetSpecies("panda").SkillsAt(24, 0), k => k.Id == "bamboo-snack");
            Assert.Contains(BattlePets.GetSpecies("panda").SkillsAt(25, 0), k => k.Id == "bamboo-snack");

            // the eggs that were there before stay the most common where they are found
            Assert.Equal("wolf-pup", PetEggs.Table(EggSource.Dungeon, 0).OrderByDescending(t => t.Weight).First().Species.Id);
            Assert.Equal("wolf-pup", PetEggs.Table(EggSource.Raid, 0).OrderByDescending(t => t.Weight).First().Species.Id);
            Assert.Equal("practice-dummy", PetEggs.Table(EggSource.Tower, 4).OrderByDescending(t => t.Weight).First().Species.Id);
            // and the rarest animals are rare: the lion is under 5% of a raid's eggs, and not in the easy dungeons
            var raid = PetEggs.Table(EggSource.Raid, 0).ToList();
            Assert.True(raid.First(t => t.Species.Id == "lion").Weight / raid.Sum(t => t.Weight) < 0.05);
            Assert.DoesNotContain(PetEggs.Table(EggSource.Dungeon, 2), t => t.Species.Id == "lion");
            Assert.DoesNotContain(PetEggs.Table(EggSource.Tower, 2), t => t.Species.Id == "penguin");
            Assert.Contains(PetEggs.Table(EggSource.Tower, 3), t => t.Species.Id == "penguin");
            // evolving takes the element's gem, or the role's
            Assert.Equal(GemKind.Ruby, PetEvolution.GemFor(BattlePets.GetSpecies("tiger")));
            Assert.Equal(GemKind.Sapphire, PetEvolution.GemFor(BattlePets.GetSpecies("capybara")));
            Assert.Equal(GemKind.Topaz, PetEvolution.GemFor(BattlePets.GetSpecies("sheep")));
            Assert.Equal(GemKind.Emerald, PetEvolution.GemFor(BattlePets.GetSpecies("rat")));
            Assert.Equal(GemKind.Onyx, PetEvolution.GemFor(BattlePets.GetSpecies("cat")));
            Assert.Equal(GemKind.Amethyst, PetEvolution.GemFor(BattlePets.GetSpecies("dog")));
        }

        [Fact]
        public void Evolving_costs_the_pets_own_gems()
        {
            Assert.Equal(GemKind.Ruby, PetEvolution.GemFor(BattlePets.GetSpecies("dragon-spark")));
            Assert.Equal(GemKind.Sapphire, PetEvolution.GemFor(BattlePets.GetSpecies("shell")));
            Assert.Equal(GemKind.Emerald, PetEvolution.GemFor(BattlePets.GetSpecies("snakelet")));
            Assert.Equal(GemKind.Diamond, PetEvolution.GemFor(BattlePets.GetSpecies("wolf-pup")));
            Assert.Equal(GemKind.Onyx, PetEvolution.GemFor(BattlePets.GetSpecies("practice-dummy")));
            Assert.Equal(10, PetEvolution.GemsFor(0, 0));
            Assert.Equal(30, PetEvolution.GemsFor(1, 0));
            Assert.Equal(18, PetEvolution.GemsFor(1, 999)); // the Rookery: 40% fewer at the top
            Assert.Equal(0, PetEvolution.GemsFor(2, 0)); // fully evolved
            Assert.Equal("10 Ruby", PetEvolution.Describe(BattlePets.GetSpecies("dragon-spark"), 0, 0));
            // hatching: the species' minutes, a quarter faster with the Rookery at the top
            var wolf = BattlePets.GetSpecies("wolf-pup");
            Assert.Equal(60, PetEggs.HatchMinutes(wolf, 0), 6);
            Assert.Equal(45, PetEggs.HatchMinutes(wolf, 999), 6);
        }

        [Fact]
        public void Strikes_fit_the_models()
        {
            // a bolt pet keeps its distance and fights with magic; a pounce or bite pet fights up close
            var raven = BattlePets.GetSpecies("companion-raven");
            var cat = BattlePets.GetSpecies("companion-cat");
            Assert.Equal(PetStrike.Bolt, raven.Strike);
            Assert.Equal(AttackStyle.Magic, raven.Stats.Style);
            Assert.True(raven.Reach > 4);
            Assert.Equal(PetStrike.Pounce, cat.Strike);
            Assert.True(cat.Reach < 3);
            Assert.Equal("enemy:Purple Metalon", BattlePets.GetSpecies("metalon").ModelAt(1));
            Assert.Equal("enemy:Red Dragon", BattlePets.GetSpecies("red-dragon").ModelAt(2));
        }

        [Fact]
        public void Taming_catches_up_then_slows()
        {
            Assert.Equal(0.25, BattlePets.TamingShare(1, 600), 6);
            Assert.Equal(0.25, BattlePets.TamingShare(599, 600), 6);
            Assert.Equal(0.10, BattlePets.TamingShare(600, 600), 6);
            Assert.Equal(0.10, BattlePets.TamingShare(700, 50), 6);
        }

        [Fact]
        public void The_old_eggs_stay_as_common_with_the_animals_in_the_tables()
        {
            // a raid's Wolf Pup egg: 10 of the 36 the old eggs weigh, at the raid's 2%
            var table = PetEggs.Table(EggSource.Raid, 0).ToList();
            var all = table.Sum(t => t.Weight);
            var wolf = table.Where(t => t.Species.Id == "wolf-pup").Sum(t => t.Weight);
            var old = table.Where(t => !t.Species.StageModels.Any(m => m.StartsWith(BattlePets.AnimalModels))).Sum(t => t.Weight);
            Assert.True(all > old);
            Assert.Equal(PetEggs.RaidChance * wolf / old, PetEggs.Chance(EggSource.Raid, 0) * wolf / all, 10);
            // every tier of every rolled source: the old kinds together keep the old chance
            foreach (var source in new[] { EggSource.Dungeon, EggSource.Raid, EggSource.Tower, EggSource.Quest, EggSource.Regatta, EggSource.PirateRaid, EggSource.Ravebridge })
            {
                for (var tier = 0; tier <= 4; ++tier)
                {
                    var t = PetEggs.Table(source, tier).ToList();
                    var sum = t.Sum(x => x.Weight);
                    var before = t.Where(x => !x.Species.StageModels.Any(m => m.StartsWith(BattlePets.AnimalModels))).Sum(x => x.Weight);
                    if (before <= 0) continue;
                    Assert.Equal(1.0, PetEggs.Widened(source, tier) * before / sum, 10);
                }
            }
        }
    }
}
