using RavenNest.Combat;
using Xunit;

namespace RavenNest.Combat.Tests
{
    public class GearStatsTests
    {
        [Fact]
        public void The_classic_columns_map_to_accuracy_power_and_armour()
        {
            var s = GearStats.FromLegacy(weaponAim: 95, weaponPower: 120, rangedAim: 0, rangedPower: 0, magicAim: 0, magicPower: 0, armorPower: 7);
            Assert.Equal(95, s[GearStat.MeleeAccuracy]);
            Assert.Equal(120, s[GearStat.MeleePower]);
            Assert.Equal(7, s[GearStat.Armor]);
            Assert.Equal("MELEE_POWER:120;MELEE_ACCURACY:95;ARMOR:7", s.ToString());
        }

        [Fact]
        public void Text_round_trips_with_shares_as_percentages()
        {
            var s = GearStats.Parse("MAGIC_POWER:300;FIRE_DAMAGE:30%;ICE_RESISTANCE:-15%;CRIT_CHANCE:5%");
            Assert.Equal(300, s[GearStat.MagicPower]);
            Assert.Equal(0.3, s[GearStat.FireDamage], 6);
            Assert.Equal(-0.15, s[GearStat.IceResistance], 6);
            Assert.Equal(0.05, s[GearStat.CritChance], 6);
            Assert.Equal("MAGIC_POWER:300;CRIT_CHANCE:5%;FIRE_DAMAGE:30%;ICE_RESISTANCE:-15%", s.ToString());
            Assert.Equal(s.ToString(), GearStats.Parse(s.ToString()).ToString());
        }

        [Fact]
        public void Old_names_unknown_stats_and_broken_entries()
        {
            var s = GearStats.Parse("weapon_aim:10; WEAPON_POWER:20;SHADOW_DAMAGE:12%;nonsense;ARMOR:abc;;");
            Assert.Equal(10, s[GearStat.MeleeAccuracy]);
            Assert.Equal(20, s[GearStat.MeleePower]);
            Assert.Equal(0, s[GearStat.Armor]);
            // a stat from a newer server is kept, not lost
            Assert.Equal("MELEE_POWER:20;MELEE_ACCURACY:10;SHADOW_DAMAGE:12%", s.ToString());
            Assert.True(GearStats.Parse(null).IsEmpty);
            Assert.True(GearStats.Parse("  ").IsEmpty);
        }

        [Fact]
        public void A_weapons_element_converts_its_own_styles_hits()
        {
            var staff = GearStats.Parse("MAGIC_POWER:200;MAGIC_ACCURACY:150;FIRE_DAMAGE:60%");
            Assert.Equal(AttackStyle.Magic, staff.WeaponStyle());

            var sheet = new CombatSheet();
            staff.ApplyTo(sheet, staff.WeaponStyle());
            Assert.Equal(200, sheet.Magic.Power);
            Assert.Equal(150, sheet.Magic.Accuracy);
            Assert.Equal(0.6, sheet.Magic.Conversion.Fire, 6);
            Assert.Equal(0, sheet.Melee.Conversion.Fire);
            Assert.Equal(0, sheet.ElementBonus.Fire);
        }

        [Fact]
        public void Element_damage_on_other_gear_adds_to_every_hit()
        {
            var ring = GearStats.Parse("POISON_DAMAGE:5%;POISON_RESISTANCE:20%;MAX_HEALTH:50;EVASION:12;CRIT_DAMAGE:25%");
            Assert.Null(ring.WeaponStyle());

            var sheet = new CombatSheet { MaxHealth = 100 };
            ring.ApplyTo(sheet, null);
            Assert.Equal(0.05, sheet.ElementBonus.Poison, 6);
            Assert.Equal(0.2, sheet.Resistance.Poison, 6);
            Assert.Equal(150, sheet.MaxHealth);
            Assert.Equal(12, sheet.GearEvasion);
            Assert.Equal(CombatMath.BaseCritDamage + 0.25, sheet.CritDamage, 6);
        }

        [Fact]
        public void A_fire_sword_deals_fire_that_resistance_takes_off()
        {
            var plain = new CombatSheet { CombatLevel = 100 };
            plain.Melee.DamageLevel = 100; plain.Melee.AimLevel = 100;
            var fiery = plain.Copy();
            GearStats.Parse("FIRE_DAMAGE:100%").ApplyExtras(fiery, AttackStyle.Melee);

            var fireproof = new CombatSheet { CombatLevel = 1, MaxHealth = 100000 };
            GearStats.Parse("FIRE_RESISTANCE:100%").ApplyTo(fireproof, null); // a player caps at 75%; this one is not a player
            var random = new SeededCombatRandom(1);
            var immune = 0;
            for (var i = 0; i < 50; ++i)
            {
                var blow = CombatMath.Attack(fiery, AttackStyle.Melee, fireproof, random);
                Assert.Equal(0, blow.Total);
                if (blow.Immune) immune++;
            }
            Assert.True(immune > 0);

            // the same sword without the element does hurt
            var hurt = 0;
            for (var i = 0; i < 50; ++i) hurt += CombatMath.Attack(plain, AttackStyle.Melee, fireproof, random).Total;
            Assert.True(hurt > 0);
        }
    }
}
