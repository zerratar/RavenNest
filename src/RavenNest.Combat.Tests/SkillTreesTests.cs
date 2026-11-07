using RavenNest.Combat;
using System.Linq;
using Xunit;

namespace RavenNest.Combat.Tests
{
    public class SkillTreesTests
    {
        private static int All(CombatSkill skill) => 999;
        private static int Low(CombatSkill skill) => 50;

        [Fact]
        public void Points_come_from_total_combat_levels_and_never_fill_every_tree()
        {
            Assert.Equal(0, SkillTrees.EarnedPoints(8));
            Assert.Equal(10, SkillTrees.EarnedPoints(100));
            Assert.Equal(100, SkillTrees.EarnedPoints(1000));
            Assert.Equal(274, SkillTrees.EarnedPoints(8 * 999));
            Assert.True(SkillTrees.TotalCost > SkillTrees.EarnedPoints(8 * 999) * 1.4, "the trees cost " + SkillTrees.TotalCost);
        }

        [Fact]
        public void Every_tree_has_actives_and_passives_and_ids_are_unique()
        {
            foreach (SkillTree tree in System.Enum.GetValues(typeof(SkillTree)))
            {
                var nodes = SkillTrees.Of(tree).ToList();
                Assert.Contains(nodes, n => n.IsActive);
                Assert.Contains(nodes, n => n.Passive != null);
                foreach (var n in nodes.Where(n => n.Passive != null)) Assert.False(GearStats.Parse(n.Passive).IsEmpty, n.Id);
                foreach (var n in nodes.Where(n => n.Passive != null)) Assert.Null(GearStats.Parse(n.Passive).Unknown);
            }
            Assert.Equal(SkillTrees.All.Count, SkillTrees.All.Select(n => n.Id).Distinct().Count());
        }

        [Fact]
        public void Raising_needs_the_level_and_the_points()
        {
            var build = new SkillBuild();
            Assert.Null(build.Raise("brute-force", Low, 10));
            Assert.Equal(1, build.Rank("brute-force"));
            Assert.Contains("needs Strength 300", build.CanRaise("whirlwind", Low, 10));
            Assert.Contains("costs 3 points", build.CanRaise("cleave", Low, 2));
            Assert.Null(build.Raise("cleave", Low, 10));
            Assert.Equal(4, build.Spent);
            for (var i = 0; i < 4; ++i) build.Raise("brute-force", Low, 100);
            Assert.Contains("highest rank", build.CanRaise("brute-force", Low, 100));
        }

        [Fact]
        public void The_loadout_takes_learned_actives_and_battle_rhythm_adds_a_slot()
        {
            var build = new SkillBuild();
            build.Raise("cleave", All, 100);
            Assert.Contains("not learned", build.SetLoadout(0, "taunt"));
            Assert.Contains("passive", build.SetLoadout(0, "brute-force"));
            Assert.Null(build.SetLoadout(0, "cleave"));
            Assert.Equal(4, build.LoadoutSlots);
            Assert.NotNull(build.SetLoadout(4, "cleave"));
            build.Raise("battle-rhythm", All, 100);
            Assert.Equal(5, build.LoadoutSlots);
            Assert.Null(build.SetLoadout(4, "cleave")); // moves it from slot 1 to slot 5
            Assert.Equal(new[] { "", "", "", "", "cleave" }, build.Loadout);
            Assert.Single(build.Actives());
        }

        [Fact]
        public void Passives_add_up_by_rank_and_the_text_round_trips()
        {
            var build = new SkillBuild();
            for (var i = 0; i < 3; ++i) build.Raise("sharp-edge", All, 100);
            build.Raise("titan", All, 100);
            var stats = build.Passives();
            Assert.Equal(0.03, stats[GearStat.CritChance], 6);
            Assert.Equal(0.10, stats[GearStat.MeleePowerBonus], 6);
            build.Raise("cleave", All, 100);
            build.SetLoadout(1, "cleave");
            var text = build.ToString();
            Assert.Equal("cleave:1;sharp-edge:3;titan:1|,cleave", text);
            var again = SkillBuild.Parse(text);
            Assert.Equal(text, again.ToString());
            Assert.Equal(build.Spent, again.Spent);
            Assert.Equal(0, SkillBuild.Parse("gone-in-an-update:3|gone-in-an-update").Spent);
        }

        [Fact]
        public void Use_rules_are_kept_with_the_build_and_the_default_clears_them()
        {
            var build = new SkillBuild();
            build.Raise("cleave", All, 100);
            build.SetLoadout(0, "cleave");
            Assert.Equal(SkillUse.InFight, build.RuleOf(SkillTrees.Get("cleave")));
            Assert.Null(build.SetRule("cleave", SkillUse.OnBoss));
            Assert.Equal("cleave:1|cleave|cleave:OnBoss", build.ToString());
            var again = SkillBuild.Parse(build.ToString());
            Assert.Equal(SkillUse.OnBoss, again.RuleOf(SkillTrees.Get("cleave")));
            again.SetRule("cleave", SkillUse.InFight);
            Assert.Equal("cleave:1|cleave", again.ToString());
            Assert.NotNull(build.SetRule("brute-force", SkillUse.OnBoss));
        }

        [Fact]
        public void Passive_bonuses_change_the_blow()
        {
            var a = new CombatSheet { CombatLevel = 100, CritChance = 0 };
            a.Melee.DamageLevel = 100; a.Melee.AimLevel = 5000; a.Melee.Power = 100;
            var d = new CombatSheet { MaxHealth = 100000, IsBoss = true };
            var random = new SeededCombatRandom(4);
            double Avg(CombatSheet x) => Enumerable.Range(0, 500).Average(_ => CombatMath.Attack(x, AttackStyle.Melee, d, random).Total);
            var plain = Avg(a);
            var boosted = a.Copy();
            GearStats.Parse("MELEE_POWER_BONUS:50%;BOSS_DAMAGE:20%").ApplyExtras(boosted, null);
            Assert.True(Avg(boosted) > plain * 1.3);

            var blocker = new CombatSheet { MaxHealth = 100000 };
            GearStats.Parse("BLOCK_CHANCE:50%").ApplyExtras(blocker, null);
            var blocked = Enumerable.Range(0, 400).Select(_ => CombatMath.Attack(a, AttackStyle.Melee, blocker, random)).Count(r => r.Blocked);
            Assert.InRange(blocked, 150, 250);
        }
    }
}
