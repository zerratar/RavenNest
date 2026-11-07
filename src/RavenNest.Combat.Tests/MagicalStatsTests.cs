using RavenNest.Combat;
using Xunit;

namespace RavenNest.Combat.Tests
{
    /// <summary>The magical gear stats (thorns, threat, damage reduction, life steal, attack speed, armour penetration) and armour on elements.</summary>
    public class MagicalStatsTests
    {
        private sealed class Fixed : ICombatRandom
        {
            private readonly double value;
            public Fixed(double value) => this.value = value;
            public double Next() => value;
        }

        private static CombatSheet Fighter(int level = 100)
        {
            var s = new CombatSheet { CombatLevel = level, MaxHealth = 100000, DefenseLevel = 1 };
            s.Melee.DamageLevel = level;
            s.Melee.AimLevel = level * 10; // always hits with the fixed rolls below
            s.CritChance = 0;
            return s;
        }

        // a roll of 0.01 hits, takes the max hit band and never crits (crit chance 0)
        private static AttackResult Blow(CombatSheet a, CombatSheet d, AttackStyle style = AttackStyle.Melee) =>
            CombatMath.Attack(a, style, d, new Fixed(0.01));

        [Fact]
        public void Armour_takes_half_its_share_off_element_damage()
        {
            var physical = Fighter();
            var fire = Fighter();
            fire.Melee.Conversion = ElementValues.Of(Element.Fire, 1);
            var armoured = Fighter();
            armoured.Armor = 640; // takes 50% off physical

            var bare = Blow(physical, Fighter()).Total;
            Assert.Equal(bare * 0.5, Blow(physical, armoured).Total, 0);
            Assert.Equal(bare * 0.75, Blow(fire, armoured).Total, 0); // half of armour's 50%
        }

        [Fact]
        public void Thorns_hurt_melee_attackers_only()
        {
            var a = Fighter();
            var d = Fighter();
            GearStats.Parse("THORNS:20%").ApplyTo(d, null);
            var melee = Blow(a, d);
            Assert.Equal((int)System.Math.Round(melee.Total * 0.2), melee.Reflected);

            a.Ranged.DamageLevel = 100; a.Ranged.AimLevel = 1000;
            Assert.Equal(0, Blow(a, d, AttackStyle.Ranged).Reflected);

            GearStats.Parse("THORNS:90%").ApplyTo(d, null); // 110% in all, capped at 50%
            var capped = Blow(a, d);
            Assert.Equal((int)System.Math.Round(capped.Total * 0.5), capped.Reflected);
        }

        [Fact]
        public void Life_steal_heals_a_share_of_the_blow()
        {
            var a = Fighter();
            GearStats.Parse("LIFE_STEAL:10%").ApplyTo(a, null);
            var blow = Blow(a, Fighter());
            Assert.Equal((int)System.Math.Round(blow.Total * 0.1), blow.Healed);
        }

        [Fact]
        public void Damage_reduction_is_capped_at_half()
        {
            var a = Fighter();
            var bare = Blow(a, Fighter()).Total;
            var d = Fighter();
            GearStats.Parse("DAMAGE_REDUCTION:20%").ApplyTo(d, null);
            Assert.Equal(bare * 0.8, Blow(a, d).Total, 0);

            var wall = Fighter();
            GearStats.Parse("DAMAGE_REDUCTION:90%").ApplyTo(wall, null);
            Assert.Equal(bare * 0.5, Blow(a, wall).Total, 0);
        }

        [Fact]
        public void Armour_penetration_ignores_a_share_of_armour()
        {
            var a = Fighter();
            var d = Fighter();
            d.Armor = 640;
            var before = Blow(a, d).Total;
            GearStats.Parse("ARMOR_PENETRATION:50%").ApplyTo(a, null); // 320 armour left: a third off instead of half
            var after = Blow(a, d).Total;
            Assert.True(after > before);
            Assert.Equal(before / 0.5 * (640.0 / 960.0), after, 0);
        }

        [Fact]
        public void Threat_and_attack_speed_go_on_the_sheet()
        {
            var s = Fighter();
            GearStats.Parse("THREAT:25%;ATTACK_SPEED:10%").ApplyTo(s, null);
            Assert.Equal(1.25, s.ThreatMultiplier, 6);
            Assert.Equal(1.1, s.AttackSpeed, 6);
            GearStats.Parse("ATTACK_SPEED:500%").ApplyTo(s, null);
            Assert.Equal(CombatMath.MaxAttackSpeed, s.AttackSpeed, 6);
            Assert.Equal("THORNS:5%;THREAT:10%;DAMAGE_REDUCTION:3%;LIFE_STEAL:2%;ATTACK_SPEED:4%;ARMOR_PENETRATION:8%",
                GearStats.Parse("ARMOR_PENETRATION:8%;ATTACK_SPEED:4%;LIFE_STEAL:2%;DAMAGE_REDUCTION:3%;THREAT:10%;THORNS:5%").ToString());
        }
    }
}
