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
            Assert.Equal("Red Whelp needs Taming 100.", Taming.CannotTame(50, dragon, "Red Dragon"));
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
            Assert.All(PetEggs.Table(EggSource.Regatta, 0), t => Assert.Contains(t.Species.Id, new[] { "shell", "sun-blossom" }));
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
            Assert.Equal(8, eggs.Count);
            // all 24 cute series models, each used once, in three stages per chain
            var models = eggs.SelectMany(s => s.StageModels).ToList();
            Assert.Equal(24, models.Distinct().Count());
            Assert.All(eggs, s => Assert.Equal(3, s.StageModels.Length));
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
    }
}
