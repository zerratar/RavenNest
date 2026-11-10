using RavenNest.Combat;
using System.Text.Json;
using Xunit;

namespace RavenNest.Combat.Tests
{
    public class EnemyDefinitionTests
    {
        private static EnemyDefinition Gladiator() => new EnemyDefinition
        {
            Id = "atria-gladiator", Name = "Gladiator",
            Attack = 458, Strength = 458, Defense = 458, Health = 467, Ranged = 4, Magic = 4,
        };

        [Fact]
        public void Combat_level_uses_the_characters_formula()
        {
            // (458 + 458 + 458 + 467) / 4 + (4 + 4 + 1) / 8 = 460.25 + 1.125
            Assert.Equal(461, Gladiator().CombatLevel);
            Assert.Equal(3, new EnemyDefinition().CombatLevel);
        }

        [Fact]
        public void The_sheet_carries_levels_health_and_style()
        {
            var sheet = Gladiator().Sheet();
            Assert.Equal(4670, sheet.MaxHealth);
            Assert.Equal(458, sheet.Melee.DamageLevel);
            Assert.Equal(458, sheet.Melee.AimLevel);
            Assert.Equal(458, sheet.DefenseLevel);
            Assert.False(sheet.IsPlayer);
        }

        [Fact]
        public void Power_and_accuracy_go_on_the_enemys_own_style()
        {
            var archer = new EnemyDefinition { Ranged = 50, Style = AttackStyle.Ranged, Power = 30, Accuracy = 20 };
            var sheet = archer.Sheet();
            Assert.Equal(30, sheet.Ranged.Power);
            Assert.Equal(20, sheet.Ranged.Accuracy);
            Assert.Equal(0, sheet.Melee.Power);
        }

        [Fact]
        public void An_element_share_converts_part_of_its_hits()
        {
            var dragon = new EnemyDefinition { Element = Element.Fire, ElementShare = 0.4, Resistance = ElementValues.Of(Element.Fire, 1) };
            var sheet = dragon.Sheet();
            Assert.Equal(0.4, sheet.Melee.Conversion.Fire, 6);
            Assert.Equal(1, sheet.Resistance.Fire, 6);
        }

        [Fact]
        public void An_element_without_a_share_stays_physical()
        {
            var sheet = new EnemyDefinition { Element = Element.Ice }.Sheet();
            Assert.Equal(0, sheet.Melee.Conversion.Total, 6);
        }

        [Fact]
        public void A_fire_immune_enemy_takes_no_fire_damage()
        {
            var attacker = new CombatSheet { CombatLevel = 100, DefenseLevel = 100 };
            attacker.Melee.DamageLevel = 100;
            attacker.Melee.AimLevel = 1000; // always hits
            attacker.Melee.Conversion = ElementValues.Of(Element.Fire, 1);
            var dragon = new EnemyDefinition { Health = 100, Resistance = ElementValues.Of(Element.Fire, 1) }.Sheet();
            var random = new SeededCombatRandom(3);
            for (var i = 0; i < 50; i++)
                Assert.Equal(0, CombatMath.Attack(attacker, AttackStyle.Melee, dragon, random).Damage.Fire, 6);
        }

        [Fact]
        public void It_round_trips_through_json()
        {
            var options = new JsonSerializerOptions { IncludeFields = true };
            var original = Gladiator();
            original.Resistance = ElementValues.Of(Element.Poison, 1);
            original.Skills = new[] { "poison-bite" };
            var copy = JsonSerializer.Deserialize<EnemyDefinition>(JsonSerializer.Serialize(original, options), options);
            Assert.Equal(original.Id, copy.Id);
            Assert.Equal(original.Health, copy.Health);
            Assert.Equal(1, copy.Resistance.Poison, 6);
            Assert.Equal("poison-bite", copy.Skills[0]);
        }

        [Fact]
        public void Bosses_roll_an_element_or_stay_physical()
        {
            Assert.Equal(Element.Physical, EnemyDefinition.BossElement(0));
            Assert.Equal(Element.Physical, EnemyDefinition.BossElement(0.39));
            Assert.Equal(Element.Fire, EnemyDefinition.BossElement(0.41));
            Assert.Equal(Element.Poison, EnemyDefinition.BossElement(0.9999));
            var fire = Gladiator().WithElement(Element.Fire, 0.4);
            Assert.Equal(0.4, fire.ElementShare, 6);
            Assert.Equal(0.5, fire.Resistance.Fire, 6);
            Assert.Equal(-0.25, fire.Resistance.Ice, 6);
            Assert.Equal(0.4, fire.Sheet().Melee.Conversion.Fire, 6);
            Assert.Equal(0, Gladiator().WithElement(Element.Physical, 0.4).ElementShare, 6);
        }
    }
}
