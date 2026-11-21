using RavenNest.Combat;
using System;
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
        public void A_part_of_a_name_only_finds_a_skill_when_it_can_mean_one()
        {
            // "shield" took the first of three and !learn spent the points on it
            Assert.Null(SkillTrees.Find("shield"));
            Assert.True(SkillTrees.Matches("shield").Count > 1);
            Assert.Contains("could mean", SkillTrees.NotFoundText("shield"));
            Assert.Contains("Shield Wall", SkillTrees.NotFoundText("shield"));
            Assert.Equal("Cleave", SkillTrees.Find("cleave").Name);
            Assert.Equal("Cleave", SkillTrees.Find("cleav").Name);
            Assert.Null(SkillTrees.Find("clea")); // Cleave or Cleanse
            Assert.Equal("Shield Wall", SkillTrees.Find("shield wall").Name);
            Assert.Equal("Shield Wall", SkillTrees.Find("shield-wall").Name);
            Assert.Null(SkillTrees.Find("no such skill"));
            Assert.StartsWith("There is no skill called no such skill.", SkillTrees.NotFoundText("no such skill"));
            Assert.Equal("Vitality", SkillTrees.SkillName(CombatSkill.Health));
            Assert.Equal("Ranged", SkillTrees.SkillName(CombatSkill.Ranged));
        }

        [Fact]
        public void Every_tree_has_one_capstone_for_a_maxed_skill()
        {
            foreach (SkillTree tree in Enum.GetValues(typeof(SkillTree)))
            {
                var capstones = SkillTrees.Of(tree).Where(n => n.Capstone).ToList();
                Assert.Single(capstones);
                var c = capstones[0];
                Assert.Equal(999, c.RequiredLevel);
                Assert.Equal(1, c.MaxRank);
                Assert.Equal(SkillTrees.CapstoneCost, c.Cost);
                Assert.False(c.IsActive);
                // its path: a node of the tier below, in the same tree
                Assert.NotEmpty(c.Requires);
                Assert.All(c.Requires, r => { var n = SkillTrees.Get(r); Assert.NotNull(n); Assert.Equal(tree, n.Tree); Assert.Equal(3, n.Tier); });
                // stats the game knows, or Beastmaster's packmate
                Assert.True(c.PetTwin || !GearStats.Parse(c.Passive).IsEmpty, c.Id);
            }
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
            build.Raise("hardy", All, 100);
            build.Raise("regeneration", All, 100);
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
            Assert.Contains("needs Executioner first", build.CanRaise("titan", All, 100));
            build.Ranks["titan"] = 1; // learned before the paths: kept
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

        [Fact]
        public void Paths_go_one_tier_down_in_the_same_tree_and_hold_up_what_is_above()
        {
            foreach (var node in SkillTrees.All)
            {
                if (node.Tier == 0) { Assert.Empty(node.Requires); continue; }
                Assert.NotEmpty(node.Requires);
                foreach (var r in node.Requires)
                {
                    var below = SkillTrees.Get(r);
                    Assert.NotNull(below);
                    Assert.Equal(node.Tree, below.Tree);
                    Assert.Equal(node.Tier - 1, below.Tier);
                }
            }

            var build = new SkillBuild();
            Assert.Contains("needs Regeneration or Bloodthirst first", build.CanRaise("battle-rhythm", All, 100));
            Assert.Null(build.Raise("hardy", All, 100));
            Assert.Null(build.Raise("bloodthirst", All, 100));
            Assert.Null(build.Raise("battle-rhythm", All, 100));
            Assert.Contains("Battle Rhythm needs Bloodthirst", build.CanLower("bloodthirst"));
            Assert.Null(build.Raise("regeneration", All, 100));
            Assert.Null(build.CanLower("bloodthirst")); // Regeneration holds the path now
        }

        [Fact]
        public void Every_node_says_its_numbers()
        {
            Assert.Equal("1% less damage taken a rank", SkillTrees.Get("steadfast").EffectText());
            Assert.Equal("2% now, 5% at 5/5", SkillTrees.Get("steadfast").TotalText(2));
            Assert.Equal("80% of a hit (up to 3 enemies in front)", SkillTrees.Get("cleave").EffectText());
            Assert.Equal("taunted for 4 s (up to 8 enemies within 6 m)", SkillTrees.Get("taunt").EffectText());
            foreach (var node in SkillTrees.All)
            {
                if (node.ExtraLoadoutSlot || node.LastStand || node.ExtraPetSkillSlot || node.HealsReachPet || node.PetTwin) continue;
                Assert.False(string.IsNullOrEmpty(node.EffectText()), node.Id);
            }
        }

        // ---------------------------------------------------------------- Beastmaster (Taming)

        [Fact]
        public void Taming_earns_points_like_one_combat_skill_and_stays_out_of_the_rest()
        {
            Assert.Equal(0, SkillTrees.TamingPoints(0));
            Assert.Equal(0, SkillTrees.TamingPoints(9));
            Assert.Equal(1, SkillTrees.TamingPoints(10));
            Assert.Equal(7, SkillTrees.TamingPoints(76));
            Assert.Equal(12, SkillTrees.TamingPoints(125));
            Assert.Equal(16, SkillTrees.TamingPoints(300));
            Assert.Equal(34, SkillTrees.TamingPoints(999));
            // on top of the combat levels' points, which stay what they were
            Assert.Equal(274, SkillTrees.EarnedPoints(8 * 999));
            Assert.Equal(274, SkillTrees.EarnedPoints(8 * 999, 1));
            Assert.Equal(281, SkillTrees.EarnedPoints(8 * 999, 76));
            Assert.Equal(308, SkillTrees.EarnedPoints(8 * 999, 999));
            // never fewer points for a higher level
            for (var level = 1; level < 999; ++level) Assert.True(SkillTrees.TamingPoints(level + 1) >= SkillTrees.TamingPoints(level));
            // and still nobody fills every tree
            Assert.True(SkillTrees.TotalCost > SkillTrees.EarnedPoints(8 * 999, 999) * 1.4, "the trees cost " + SkillTrees.TotalCost);
        }

        [Fact]
        public void Beastmaster_is_a_tree_like_the_others_gated_by_Taming()
        {
            Assert.Equal(CombatSkill.Taming, SkillTrees.SkillOf(SkillTree.Beastmaster));
            Assert.Equal("Taming", SkillTrees.SkillName(CombatSkill.Taming));
            var nodes = SkillTrees.Of(SkillTree.Beastmaster).ToList();
            // the size and the price of the other trees
            var sizes = Enum.GetValues(typeof(SkillTree)).Cast<SkillTree>().Where(t => t != SkillTree.Beastmaster).Select(t => SkillTrees.Of(t).Count()).ToList();
            var costs = Enum.GetValues(typeof(SkillTree)).Cast<SkillTree>().Where(t => t != SkillTree.Beastmaster).Select(t => SkillTrees.Of(t).Sum(n => n.Cost * n.MaxRank)).ToList();
            Assert.InRange(nodes.Count, sizes.Min(), sizes.Max() + 1);
            Assert.InRange(nodes.Sum(n => n.Cost * n.MaxRank), costs.Min(), (int)(costs.Max() * 1.1));
            // three nodes a tier and the capstone, an active for the loadout from the first tier
            for (var tier = 0; tier < 4; ++tier) Assert.Equal(3, nodes.Count(n => n.Tier == tier));
            Assert.Equal(3, nodes.Count(n => n.IsActive));
            Assert.Contains(nodes, n => n.IsActive && n.Tier == 0);
            // gated by Taming, whatever the combat levels
            Func<CombatSkill, int> fighter = skill => skill == CombatSkill.Taming ? 76 : 999;
            var build = new SkillBuild();
            Assert.Null(build.Raise("fang-and-claw", fighter, 300));
            Assert.Null(build.Raise("sic-em", fighter, 300));
            Assert.Contains("needs Taming 100", build.CanRaise("mend-pet", fighter, 300));
            Assert.Contains("needs Taming 999", build.CanRaise("pack-leader", fighter, 300));
            Func<CombatSkill, int> tamer = skill => skill == CombatSkill.Taming ? 999 : 1;
            Assert.Contains("needs Strength 100", build.CanRaise("crushing-blow", tamer, 300));
            Assert.Null(build.Raise("mend-pet", tamer, 300));
            // found from chat by its names
            Assert.Equal("sic-em", SkillTrees.Find("sic 'em").Id);
            Assert.Equal("sic-em", SkillTrees.Find("sic").Id);
            Assert.Equal("guardians-oath", SkillTrees.Find("guardian").Id);
            Assert.Equal("pack-leader", SkillTrees.Find("pack leader").Id);
        }

        [Fact]
        public void Beastmaster_nodes_say_what_they_do_for_the_pet()
        {
            Assert.Equal("your pet's blows and heals are 4% stronger a rank", SkillTrees.Get("fang-and-claw").EffectText());
            Assert.Equal("5% more health for your pet a rank", SkillTrees.Get("thick-hide").EffectText());
            Assert.Equal("your pet strikes the target for 250% of its hit", SkillTrees.Get("sic-em").EffectText());
            Assert.Equal("your pet gets up 10% sooner after a knockout a rank", SkillTrees.Get("back-on-its-feet").EffectText());
            Assert.Equal("2% more health while a battle pet is out a rank", SkillTrees.Get("kinship").EffectText());
            Assert.Equal("your pet gets 30% of its health back (and back up, if it was knocked out)", SkillTrees.Get("mend-pet").EffectText());
            Assert.Equal("your pet's gathering skills are 10% stronger a rank", SkillTrees.Get("keen-senses").EffectText());
            Assert.StartsWith("3% of each of your hits more, as your pet's element", SkillTrees.Get("elemental-bond").EffectText());
            Assert.Equal("a tank pet takes 20% more of the blows meant for you", SkillTrees.Get("guardians-oath").EffectText());
            Assert.Equal("your pet attacks 50% faster for 8 s, your pet deals 30% more damage for 8 s", SkillTrees.Get("call-of-the-wild").EffectText());
            Assert.Equal("a battle pet out", SkillTrees.NeedsText(SkillGear.Pet));
            Assert.Equal("when your pet is below half health or knocked out", SkillTrees.UseText(SkillUse.PetHurt));
            Assert.Equal(SkillGear.Pet, SkillTrees.Get("sic-em").Active.Needs);
            Assert.Equal(SkillGear.None, SkillTrees.Get("mend-pet").Active.Needs); // a knocked out pet is what it is for
        }

        [Fact]
        public void A_build_carries_what_it_does_for_the_pet()
        {
            Func<CombatSkill, int> all = skill => 999;
            var build = new SkillBuild();
            Assert.Equal(0, build.ExtraPetSkillSlots);
            Assert.False(build.HealsReachPet);
            Assert.False(build.HasPetTwin);
            Assert.True(build.PetElementBonus(Element.Fire).IsEmpty);
            foreach (var id in new[] { "fang-and-claw", "fang-and-claw", "thick-hide", "sic-em", "back-on-its-feet", "kinship", "kinship", "mend-pet", "trick-training", "shared-mending",
                         "keen-senses", "elemental-bond", "elemental-bond", "guardians-oath", "call-of-the-wild", "pack-leader" })
                Assert.Null(build.Raise(id, all, 400));
            var stats = build.Passives();
            Assert.Equal(0.08, stats[GearStat.PetPower], 6);
            Assert.Equal(0.05, stats[GearStat.PetHealth], 6);
            Assert.Equal(0.10, stats[GearStat.PetRecovery], 6);
            Assert.Equal(0.10, stats[GearStat.PetGathering], 6);
            Assert.Equal(0.20, stats[GearStat.PetGuard], 6);
            // Kinship only with a pet out
            Assert.Equal(0, stats[GearStat.MaxHealthBonus], 6);
            Assert.Equal(0.04, build.Passives(petOut: true)[GearStat.MaxHealthBonus], 6);
            Assert.Equal(1, build.ExtraPetSkillSlots);
            Assert.True(build.HealsReachPet);
            Assert.True(build.HasPetTwin);
            // Elemental Bond: the pet's element on the owner's hits, crit for a pet without one
            Assert.Equal(0.06, build.PetElementBonus(Element.Fire)[GearStat.FireDamage], 6);
            Assert.Equal(0.06, build.PetElementBonus(Element.Poison)[GearStat.PoisonDamage], 6);
            Assert.Equal(0.03, build.PetElementBonus(Element.Physical)[GearStat.CritChance], 6);
            // the actives went into the loadout's text and come back, the pet's use rule too
            build.SetLoadout(0, "sic-em");
            build.SetLoadout(1, "mend-pet");
            Assert.Null(build.SetRule("sic-em", SkillUse.PetHurt));
            var again = SkillBuild.Parse(build.ToString());
            Assert.Equal(build.ToString(), again.ToString());
            Assert.Equal(SkillUse.PetHurt, again.RuleOf(SkillTrees.Get("sic-em")));
            Assert.Equal(SkillUse.PetHurt, again.RuleOf(SkillTrees.Get("mend-pet")));
            Assert.Equal(build.Spent, again.Spent);
            // a respec gives every point back, Beastmaster's too
            build.Reset();
            Assert.Equal(0, build.Spent);
            Assert.False(build.HasPetTwin);
            // the new stats read and write like the others
            Assert.Null(GearStats.Parse("PET_POWER:4%;PET_HEALTH:5%;PET_RECOVERY:10%;PET_GATHERING:10%;PET_GUARD:20%").Unknown);
        }

        [Fact]
        public void A_reader_that_does_not_know_a_node_drops_it_and_keeps_the_rest()
        {
            // what a game from before a tree does with a build that has its nodes: they are not there, nothing else moves
            var build = SkillBuild.Parse("cleave:1;some-future-node:3;brute-force:2|cleave,some-future-active|some-future-active:PetHurt,cleave:OnBoss");
            Assert.Equal(1, build.Rank("cleave"));
            Assert.Equal(2, build.Rank("brute-force"));
            Assert.Equal(0, build.Rank("some-future-node"));
            Assert.Equal(new[] { "cleave" }, build.Loadout);
            Assert.Equal(SkillUse.OnBoss, build.RuleOf(SkillTrees.Get("cleave")));
        }
    }
}
