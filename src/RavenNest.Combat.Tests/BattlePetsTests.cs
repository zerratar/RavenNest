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
        public void Menagerie_is_modest()
        {
            Assert.Equal(0, Menagerie.ExperienceBonus(0));
            Assert.Equal(0.1, Menagerie.ExperienceBonus(100), 6);
            Assert.Equal(0.5, Menagerie.ExperienceBonus(999), 6);
            Assert.Equal(0, Menagerie.ExtraStablePlaces(99));
            Assert.Equal(1, Menagerie.ExtraStablePlaces(100));
            Assert.Equal(2, Menagerie.ExtraStablePlaces(999));
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
