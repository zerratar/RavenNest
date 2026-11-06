using RavenNest.Combat;
using System;
using System.Linq;
using Xunit;

namespace RavenNest.Combat.Tests
{
    public class CombatMathTests
    {
        /// <summary>Today's max hit, copied verbatim from the game (Assets/Scripts/Core/GameMath.cs, MaxHit).</summary>
        private static int TodaysMaxHit(int level, int power)
        {
            var w1 = power * 0.00175D;
            var w2 = w1 + 0.1d;
            var w3 = (level + 3) * w2 + 1.05D;
            return (int)(w3 * 0.95d);
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(10, 5)]
        [InlineData(99, 0)]
        [InlineData(99, 120)]
        [InlineData(300, 400)]
        [InlineData(999, 854)]
        public void MaxHit_is_todays_times_ten(int level, int power)
        {
            // today's value is truncated to a whole number, the new one is not: within one old point, times ten
            var scaled = CombatMath.MaxHit(level, power) / CombatMath.DamageScale;
            var today = TodaysMaxHit(level, power);
            Assert.InRange(scaled - today, 0, 1);
        }

        [Fact]
        public void MaxHealth_is_ten_per_level()
        {
            Assert.Equal(100, CombatMath.MaxHealth(10));
            Assert.Equal(9990, CombatMath.MaxHealth(999));
            Assert.Equal(1050, CombatMath.MaxHealth(100, 50));
            Assert.Equal(1, CombatMath.MaxHealth(0));
        }

        [Theory]
        [InlineData(100, 100, 0.5)]
        [InlineData(200, 100, 0.75)]
        [InlineData(100, 200, 0.25)]
        [InlineData(1000, 0, 0.95)]
        [InlineData(0, 1000, 0.05)]
        [InlineData(0, 0, 0.5)]
        public void HitChance_even_is_half_and_never_certain(int accuracy, int evasion, double expected)
        {
            Assert.Equal(expected, CombatMath.HitChance(accuracy, evasion), 6);
        }

        [Fact]
        public void Armor_reduces_more_against_low_level_attackers_and_never_everything()
        {
            var vsLow = CombatMath.ArmorReduction(500, 10);
            var vsHigh = CombatMath.ArmorReduction(500, 300);
            Assert.True(vsLow > vsHigh);
            Assert.True(CombatMath.ArmorReduction(100000, 1) < 1);
            Assert.Equal(0, CombatMath.ArmorReduction(0, 50));
        }

        [Theory]
        [InlineData(0.0, false, 1.0)]
        [InlineData(0.3, false, 0.7)]
        [InlineData(-0.5, false, 1.5)]
        [InlineData(1.0, false, 0.0)]
        [InlineData(1.0, true, 0.25)]
        [InlineData(0.9, true, 0.25)]
        public void Resistance_caps_players_and_lets_enemies_be_weak_or_immune(double resistance, bool isPlayer, double expected)
        {
            Assert.Equal(expected, CombatMath.ResistanceMultiplier(resistance, isPlayer), 6);
        }

        [Fact]
        public void Hit_share_matches_todays_bands()
        {
            var random = new SeededCombatRandom(1);
            var shares = Enumerable.Range(0, 200000).Select(_ => CombatMath.RollHitShare(random)).ToArray();
            Assert.All(shares, x => Assert.InRange(x, 0.25, 1.0));
            Assert.InRange(shares.Count(x => x == 1.0) / (double)shares.Length, 0.045, 0.055);
            Assert.InRange(shares.Average(), 0.6, 0.75);
        }

        private static CombatSheet Fighter(int level, int power = 0, int armor = 0)
        {
            var sheet = new CombatSheet { CombatLevel = level, DefenseLevel = level, Armor = armor, MaxHealth = CombatMath.MaxHealth(level) };
            sheet.Melee.DamageLevel = level;
            sheet.Melee.AimLevel = level;
            sheet.Melee.Power = power;
            return sheet;
        }

        [Fact]
        public void Even_fighters_hit_about_half_the_time()
        {
            var a = Fighter(50);
            var b = Fighter(50);
            var random = new SeededCombatRandom(7);
            var hits = Enumerable.Range(0, 20000).Count(_ => CombatMath.Attack(a, AttackStyle.Melee, b, random).Hit);
            Assert.InRange(hits / 20000.0, 0.48, 0.52);
        }

        [Fact]
        public void A_fire_skill_against_a_fire_immune_enemy_is_immune_and_deals_nothing()
        {
            var a = Fighter(80, 100);
            var dragon = Fighter(80);
            dragon.Resistance.Fire = 1;
            var options = new AttackOptions { Multiplier = 1, Element = Element.Fire, CannotMiss = true };
            var result = CombatMath.Attack(a, AttackStyle.Melee, dragon, new SeededCombatRandom(3), options);
            Assert.True(result.Hit);
            Assert.True(result.Immune);
            Assert.Equal(0, result.Total);
        }

        [Fact]
        public void A_weakness_takes_more_than_a_plain_hit()
        {
            var a = Fighter(80, 100);
            var plain = Fighter(80);
            var weak = Fighter(80);
            weak.Resistance.Fire = -0.5;
            var options = new AttackOptions { Multiplier = 1, Element = Element.Fire, CannotMiss = true };
            double Average(CombatSheet target) =>
                Enumerable.Range(0, 5000).Average(i => CombatMath.Attack(a, AttackStyle.Melee, target, new SeededCombatRandom(i), options).Total);
            Assert.InRange(Average(weak) / Average(plain), 1.45, 1.55);
        }

        [Fact]
        public void Conversion_splits_the_blow_and_gems_add_on_top()
        {
            var mage = Fighter(80, 100);
            mage.Melee.Conversion.Fire = 0.6;
            mage.ElementBonus.Ice = 0.2;
            var target = Fighter(80);
            var result = CombatMath.Attack(mage, AttackStyle.Melee, target, new SeededCombatRandom(5), new AttackOptions { Multiplier = 1, CannotMiss = true });
            Assert.True(result.Damage.Fire > result.Damage.Physical);
            Assert.InRange(result.Damage.Ice / result.Damage.Fire, 0.3, 0.37);
        }

        [Fact]
        public void Armor_only_reduces_physical()
        {
            var a = Fighter(100, 200);
            var armored = Fighter(100, 0, 2000);
            var bare = Fighter(100);
            var physical = new AttackOptions { Multiplier = 1, CannotMiss = true };
            var fire = new AttackOptions { Multiplier = 1, CannotMiss = true, Element = Element.Fire };
            double Average(CombatSheet target, AttackOptions o) =>
                Enumerable.Range(0, 3000).Average(i => CombatMath.Attack(a, AttackStyle.Melee, target, new SeededCombatRandom(i), o).Total);
            Assert.True(Average(armored, physical) < Average(bare, physical) * 0.7);
            Assert.InRange(Average(armored, fire) / Average(bare, fire), 0.99, 1.01);
        }

        [Fact]
        public void Heals_never_miss_and_can_crit()
        {
            var healer = Fighter(100);
            healer.Healing.DamageLevel = 100;
            healer.Healing.Power = 150;
            healer.CritChance = 1;
            var heal = CombatMath.Heal(healer, new SeededCombatRandom(2));
            Assert.True(heal.Critical);
            Assert.True(heal.Amount > 0);
        }
    }

    public class StatusEffectTests
    {
        [Fact]
        public void Five_chills_freeze()
        {
            var set = new StatusEffectSet();
            for (var i = 0; i < 4; i++) set.Chill();
            Assert.False(set.CannotAct);
            Assert.InRange(set.AttackSpeedFactor, 0.19, 0.21);
            set.Chill();
            Assert.True(set.CannotAct);
            Assert.False(set.Has(StatusEffectKind.Chill));
        }

        [Fact]
        public void Poison_stacks_to_five_and_ticks_by_stacks()
        {
            var set = new StatusEffectSet();
            for (var i = 0; i < 8; i++) set.Poison(10);
            Assert.Equal(5, set.Find(StatusEffectKind.Poison).Stacks);
            var damage = set.Tick(1);
            Assert.Equal(50, damage.Poison, 6);
        }

        [Fact]
        public void Burn_deals_its_share_over_its_duration_then_ends()
        {
            var set = new StatusEffectSet();
            set.ApplyProcs(new[] { new ElementProc { Element = Element.Fire, Damage = 100 } }, new SeededCombatRandom(1));
            var total = 0.0;
            for (var i = 0; i < 10; i++) total += set.Tick(1).Fire;
            Assert.Equal(100 * ElementRules.BurnShare, total, 6);
            Assert.False(set.Has(StatusEffectKind.Burn));
        }

        [Fact]
        public void Bosses_take_half_a_stun_then_ignore_stuns_for_a_while()
        {
            var boss = new StatusEffectSet { IsBoss = true };
            Assert.True(boss.CrowdControl(StatusEffectKind.Stun, 2));
            Assert.Equal(1, boss.Find(StatusEffectKind.Stun).Remaining, 6);
            boss.Tick(1.5);
            Assert.False(boss.CrowdControl(StatusEffectKind.Stun, 2));
            boss.Tick(6);
            Assert.True(boss.CrowdControl(StatusEffectKind.Stun, 2));
        }

        [Fact]
        public void Lightning_chains_part_of_the_blow()
        {
            var set = new StatusEffectSet();
            var chained = set.ApplyProcs(new[] { new ElementProc { Element = Element.Lightning, Damage = 100 } }, new SeededCombatRandom(1));
            Assert.Equal(100 * ElementRules.ShockChainShare, chained, 6);
        }
    }

    public class ThreatTableTests
    {
        private static bool Alive(string _) => true;

        [Fact]
        public void Keeps_its_target_until_someone_clearly_passes_it()
        {
            var table = new ThreatTable<string>();
            table.AddDamage("tank", 100);
            table.AddDamage("dps", 105);
            Assert.Equal("tank", table.ChooseTarget("tank", false, Alive));
            table.AddDamage("dps", 10);
            Assert.Equal("dps", table.ChooseTarget("tank", false, Alive));
        }

        [Fact]
        public void Ranged_enemies_are_harder_to_pull()
        {
            var table = new ThreatTable<string>();
            table.AddDamage("tank", 100);
            table.AddDamage("dps", 120);
            Assert.Equal("dps", table.ChooseTarget("tank", false, Alive));
            Assert.Equal("tank", table.ChooseTarget("tank", true, Alive));
        }

        [Fact]
        public void A_taunt_forces_the_target_and_holds_after()
        {
            var table = new ThreatTable<string>();
            table.AddDamage("dps", 500);
            table.Taunt("tank", 4);
            Assert.Equal("tank", table.ChooseTarget("dps", false, Alive));
            table.Tick(5);
            Assert.Equal("tank", table.ChooseTarget("tank", false, Alive));
        }

        [Fact]
        public void Healing_threat_is_half_and_shared()
        {
            Assert.Equal(25, ThreatTable<string>.HealingThreat(100, 2), 6);
        }

        [Fact]
        public void Dead_fighters_are_never_chosen()
        {
            var table = new ThreatTable<string>();
            table.AddDamage("a", 500);
            table.AddDamage("b", 10);
            Assert.Equal("b", table.ChooseTarget("a", false, x => x != "a"));
        }
    }
}
