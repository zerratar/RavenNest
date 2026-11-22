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
            // the trade trees are passives only, rolled by the server: they have their own tests below
            foreach (var tree in SkillTrees.Trees(trade: false))
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
                // its path: a node of the tier below, in the same tree (a trade tree's also from the nodes it makes better)
                Assert.NotEmpty(c.Requires);
                Assert.All(c.Requires, r => { var n = SkillTrees.Get(r); Assert.NotNull(n); Assert.Equal(tree, n.Tree); if (!SkillTrees.IsTrade(tree)) Assert.Equal(3, n.Tier); });
                Assert.Contains(c.Requires, r => SkillTrees.Get(r).Tier == 3);
                // stats the game knows, Beastmaster's packmate, or what a trade tree's does on the server
                Assert.True(c.PetTwin || c.Trade != TradeEffect.None || !GearStats.Parse(c.Passive).IsEmpty, c.Id);
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
                    // one tier down; a trade tree's capstone also hangs from the nodes it makes better, further down
                    if (SkillTrees.IsTrade(node.Tree) && node.Capstone) Assert.True(below.Tier < node.Tier);
                    else Assert.Equal(node.Tier - 1, below.Tier);
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
            var sizes = SkillTrees.Trees(trade: false).Where(t => t != SkillTree.Beastmaster).Select(t => SkillTrees.Of(t).Count()).ToList();
            var costs = SkillTrees.Trees(trade: false).Where(t => t != SkillTree.Beastmaster).Select(t => SkillTrees.Of(t).Sum(n => n.Cost * n.MaxRank)).ToList();
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

        // ---------------------------------------------------------------- the trade trees (Gathering and Artisan)

        private static Func<CombatSkill, int> Levels(int all, params (CombatSkill Skill, int Level)[] others) => skill =>
        {
            foreach (var o in others) if (o.Skill == skill) return o.Level;
            return all;
        };

        [Fact]
        public void Trade_levels_earn_points_by_the_table_in_the_design()
        {
            // docs/trade-skills-design.md, section 3: all nine trade skills added up
            Assert.Equal(0, SkillTrees.TradePoints(0));
            Assert.Equal(0, SkillTrees.TradePoints(9));      // a new character, nine skills at 1
            Assert.Equal(1, SkillTrees.TradePoints(10));     // the first at one level gained
            Assert.Equal(9, SkillTrees.TradePoints(97));     // the median of the sample
            Assert.Equal(13, SkillTrees.TradePoints(130));
            Assert.Equal(40, SkillTrees.TradePoints(400));
            Assert.Equal(42, SkillTrees.TradePoints(783));
            Assert.Equal(44, SkillTrees.TradePoints(1007));  // one skill at 999, the rest untouched
            Assert.Equal(46, SkillTrees.TradePoints(1302));
            Assert.Equal(57, SkillTrees.TradePoints(3007));
            Assert.Equal(82, SkillTrees.TradePoints(6760));
            Assert.Equal(97, SkillTrees.TradePoints(9 * 999));
            for (var total = 0; total < 9 * 999; ++total) Assert.True(SkillTrees.TradePoints(total + 1) >= SkillTrees.TradePoints(total));
        }

        [Fact]
        public void There_is_one_pool_and_nobodys_points_from_fighting_change()
        {
            // the combat levels' and Taming's points are what they were
            Assert.Equal(274, SkillTrees.EarnedPoints(8 * 999));
            Assert.Equal(308, SkillTrees.EarnedPoints(8 * 999, 999));
            Assert.Equal(308, SkillTrees.EarnedPoints(8 * 999, 999, 9));
            // and the trade skills' come on top, into the same pool: 405 with everything at 999
            Assert.Equal(405, SkillTrees.EarnedPoints(8 * 999, 999, 9 * 999));
            Assert.Equal(405, SkillTrees.EarnedPoints(skill => 999));
            Assert.Equal(274, SkillPoints.FromCombat(skill => 999));
            Assert.Equal(34, SkillPoints.FromTaming(skill => 999));
            Assert.Equal(97, SkillPoints.FromTrade(skill => 999));
            // a new character: nothing
            Assert.Equal(0, SkillTrees.EarnedPoints(skill => 1));
            // a gatherer who never fought: Mining 999 alone pays
            var miner = Levels(1, (CombatSkill.Mining, 999));
            Assert.Equal(44, SkillTrees.EarnedPoints(miner));
            // a fighter may spend fighting's points in a trade tree once a trade skill opens it
            var fighter = Levels(999, (CombatSkill.Taming, 1), (CombatSkill.Woodcutting, 1), (CombatSkill.Mining, 1), (CombatSkill.Fishing, 120), (CombatSkill.Farming, 1),
                (CombatSkill.Gathering, 1), (CombatSkill.Crafting, 1), (CombatSkill.Cooking, 1), (CombatSkill.Alchemy, 1), (CombatSkill.Sailing, 1));
            Assert.Equal(274 + 12, SkillTrees.EarnedPoints(fighter));
            var build = new SkillBuild();
            Assert.Null(build.Raise("sure-hands", fighter));
            Assert.Null(build.Raise("bountiful", fighter));
            Assert.Null(build.Raise("cleave", fighter));
            Assert.Equal(1 + 2 + 3, build.Spent);
            Assert.Equal(build.Spent, SkillPoints.Spent(SkillPoints.Shared, build));
            Assert.Equal(SkillPoints.Shared, SkillPoints.PoolOf(SkillTree.Gathering));
            Assert.Equal(SkillPoints.PoolOf(SkillTree.Might), SkillPoints.PoolOf(SkillTree.Artisan));
            // the points run out as one: 44 earned, 40 spent, and the capstone's 15 are not there
            var poor = new SkillBuild();
            for (var i = 0; i < 5; ++i) Assert.Null(poor.Raise("sure-hands", miner));
            for (var i = 0; i < 5; ++i) Assert.Null(poor.Raise("bountiful", miner));
            for (var i = 0; i < 5; ++i) Assert.Null(poor.Raise("quick-return", miner));
            Assert.Null(poor.Raise("nest-finder", miner));
            Assert.Equal(40, poor.Spent);
            Assert.Contains("costs 15 points; you have 4", poor.CanRaise("master-of-the-trade", miner));
            // still nobody fills every tree
            Assert.True(SkillTrees.TotalCost > SkillTrees.EarnedPoints(skill => 999) * 1.4, "the trees cost " + SkillTrees.TotalCost);
        }

        [Fact]
        public void A_planners_levels_grow_by_the_nine_trade_skills_and_old_links_still_open()
        {
            Assert.Equal(18, SkillTrees.TreeSkills);
            Assert.Equal(18, Enum.GetValues(typeof(CombatSkill)).Length);
            // the order is the links' format: the eight combat skills, Taming, the nine trade skills
            Assert.Equal(8, (int)CombatSkill.Taming);
            Assert.Equal(9, (int)CombatSkill.Woodcutting);
            Assert.Equal(17, (int)CombatSkill.Sailing);
            Assert.Equal(9, SkillTrees.TradeSkills.Length);
            Assert.All(SkillTrees.TradeSkills, s => Assert.True(SkillTrees.IsTradeSkill(s)));
            Assert.All(SkillTrees.CombatSkills, s => Assert.False(SkillTrees.IsTradeSkill(s)));
            Assert.False(SkillTrees.IsTradeSkill(CombatSkill.Taming));
            // a link from before Beastmaster (eight levels), from before the trade trees (nine), and a full one
            Assert.Equal(80, SkillTrees.EarnedPoints(Enumerable.Repeat(100, 8).ToArray()));
            Assert.Equal(90, SkillTrees.EarnedPoints(Enumerable.Repeat(100, 9).ToArray()));
            Assert.Equal(90 + 40 + 3, SkillTrees.EarnedPoints(Enumerable.Repeat(100, 18).ToArray()));
            Assert.Equal(405, SkillTrees.EarnedPoints(Enumerable.Repeat(999, 18).ToArray()));
            Assert.Equal(0, SkillTrees.EarnedPoints((int[])null));
            // the missing levels are nothing: no trade node opens from an old link
            var old = SkillTrees.LevelsFrom(Enumerable.Repeat(999, 9).ToArray());
            Assert.Equal(999, old(CombatSkill.Taming));
            Assert.Equal(0, old(CombatSkill.Mining));
            Assert.Contains("needs Woodcutting, Mining, Fishing, Farming or Gathering 1.", new SkillBuild().CanRaise("sure-hands", old));
        }

        [Fact]
        public void The_trade_trees_are_seven_passives_each_at_the_designs_prices()
        {
            Assert.Equal(new[] { SkillTree.Gathering, SkillTree.Artisan }, SkillTrees.Trees(trade: true).ToArray());
            Assert.Equal(9, SkillTrees.Trees(trade: false).Count());
            int Cost(SkillTree t) => SkillTrees.Of(t).Sum(n => n.Cost * n.MaxRank);
            Assert.Equal(7, SkillTrees.Of(SkillTree.Gathering).Count());
            Assert.Equal(7, SkillTrees.Of(SkillTree.Artisan).Count());
            Assert.Equal(70, Cost(SkillTree.Gathering));
            Assert.Equal(84, Cost(SkillTree.Artisan));
            Assert.Equal(63, Cost(SkillTree.Hunter)); // without Gem Salvager's 9
            Assert.Equal(SkillTrees.Trees(trade: false).Sum(Cost) + 154, SkillTrees.TotalCost);
            foreach (var tree in SkillTrees.Trees(trade: true))
            {
                Assert.DoesNotContain(SkillTrees.Of(tree), n => n.IsActive);
                Assert.Equal(SkillTrees.Of(tree).Count(), SkillTrees.TradeNodes(tree).Count);
                foreach (var n in SkillTrees.Of(tree))
                {
                    // each does something the server can read: a trade effect, or Gem Salvager's stat
                    Assert.True(n.Trade != TradeEffect.None || n.Id == "gem-salvager", n.Id);
                    Assert.False(string.IsNullOrEmpty(n.EffectText()), n.Id);
                    Assert.DoesNotContain("{0}", n.EffectText());
                }
            }
            // ranks x cost, by tier, as the table says
            (string Id, int Tier, int Ranks, int Cost)[] table =
            {
                ("sure-hands", 0, 5, 1), ("villages-share", 0, 5, 1), ("bountiful", 1, 5, 2), ("tireless", 1, 5, 2), ("quick-return", 2, 5, 3),
                ("nest-finder", 3, 1, 10), ("master-of-the-trade", 4, 1, 15),
                ("steady-hands", 0, 5, 1), ("thrifty", 1, 5, 2), ("second-helping", 1, 5, 2), ("gem-salvager", 2, 3, 3), ("gem-cutter", 2, 5, 3),
                ("enchanters-patience", 3, 5, 4), ("master-artisan", 4, 1, 15),
            };
            foreach (var row in table)
            {
                var n = SkillTrees.Get(row.Id);
                Assert.NotNull(n);
                Assert.Equal(row.Tier, n.Tier);
                Assert.Equal(row.Ranks, n.MaxRank);
                Assert.Equal(row.Cost, n.Cost);
            }
            // found from chat by their names; none is taken for Vitality's Second Wind
            Assert.Equal("villages-share", SkillTrees.Find("village's share").Id);
            Assert.Equal("villages-share", SkillTrees.Find("villages share").Id);
            Assert.Equal("bountiful", SkillTrees.Find("bount").Id);
            Assert.Equal("tireless", SkillTrees.Find("tireless").Id);
            Assert.Equal("second-wind", SkillTrees.Find("second wind").Id);
            Assert.Equal(SkillTree.Vitality, SkillTrees.Find("second wind").Tree);
            Assert.Equal("enchanters-patience", SkillTrees.Find("enchanter").Id);
            Assert.Equal("master-of-the-trade", SkillTrees.Find("master of the trade").Id);
            Assert.Null(SkillTrees.Find("master")); // of the Trade, or Artisan
        }

        [Fact]
        public void A_trade_node_opens_with_one_of_its_trees_skills_and_works_in_each_that_is_at_its_level()
        {
            Assert.Equal(new[] { CombatSkill.Woodcutting, CombatSkill.Mining, CombatSkill.Fishing, CombatSkill.Farming, CombatSkill.Gathering }, SkillTrees.SkillsOf(SkillTree.Gathering).ToArray());
            Assert.Equal(new[] { CombatSkill.Crafting, CombatSkill.Cooking, CombatSkill.Alchemy }, SkillTrees.SkillsOf(SkillTree.Artisan).ToArray());
            Assert.Equal(new[] { CombatSkill.Strength }, SkillTrees.SkillsOf(SkillTree.Might).ToArray());
            Assert.Equal("Woodcutting, Mining, Fishing, Farming or Gathering", SkillTrees.GateName(SkillTree.Gathering));
            Assert.Equal("Crafting, Cooking or Alchemy", SkillTrees.GateName(SkillTree.Artisan));
            Assert.Equal("Strength", SkillTrees.GateName(SkillTree.Might));
            Assert.Equal("Vitality", SkillTrees.GateName(SkillTree.Vitality));

            // Farming 700, Mining 120, the rest 20: the level 100 nodes open, the level 300 one through Farming alone
            var farmer = Levels(20, (CombatSkill.Farming, 700), (CombatSkill.Mining, 120));
            Assert.Equal(700, SkillTrees.GateLevel(SkillTree.Gathering, farmer));
            Assert.Equal(CombatSkill.Farming, SkillTrees.BestSkill(SkillTree.Gathering, farmer));
            Assert.Equal(CombatSkill.Crafting, SkillTrees.BestSkill(SkillTree.Artisan, farmer)); // level: the first
            var build = new SkillBuild();
            Assert.Null(build.Raise("sure-hands", farmer, 100));
            Assert.Null(build.Raise("bountiful", farmer, 100));
            Assert.Null(build.Raise("quick-return", farmer, 100));
            Assert.Null(build.Raise("nest-finder", farmer, 100));
            Assert.Contains("needs Woodcutting, Mining, Fishing, Farming or Gathering 999.", build.CanRaise("master-of-the-trade", farmer, 100));
            // combat levels open nothing here, and trade levels nothing there
            Assert.Contains("needs Woodcutting, Mining, Fishing, Farming or Gathering 100.", new SkillBuild().CanRaise("bountiful", Levels(999, (CombatSkill.Woodcutting, 99), (CombatSkill.Mining, 99), (CombatSkill.Fishing, 99), (CombatSkill.Farming, 99), (CombatSkill.Gathering, 99)), 100));
            Assert.Contains("needs Strength 100", build.CanRaise("crushing-blow", farmer, 100));
            // the path counts as in every tree
            Assert.Contains("needs Village's Share or Sure Hands first", new SkillBuild().CanRaise("tireless", farmer, 100));

            // what works where: Bountiful (100) while farming and mining, not while fishing at 20; Quick Return (300) while farming only
            var farming = TradeSkills.Gathering(build, 700);
            var mining = TradeSkills.Gathering(build, 120);
            var fishing = TradeSkills.Gathering(build, 20);
            Assert.Equal(0.02, farming.FindChance, 6);
            Assert.Equal(0.02, farming.DoubleFind, 6);
            Assert.Equal(0.02, farming.WaitCut, 6);
            Assert.True(farming.Eggs);
            Assert.Equal(0.02, mining.FindChance, 6);
            Assert.Equal(0.02, mining.DoubleFind, 6);
            Assert.Equal(0, mining.WaitCut, 6);
            Assert.False(mining.Eggs);
            Assert.Equal(0.02, fishing.FindChance, 6); // Sure Hands is a level 1 node
            Assert.Equal(0, fishing.DoubleFind, 6);
            Assert.Equal(1, build.RankIn(SkillTrees.Get("bountiful"), 100));
            Assert.Equal(0, build.RankIn(SkillTrees.Get("bountiful"), 99));

            // the gem and enchanting nodes are Crafting's alone: Cooking 999 does not open them, but it opens the capstone
            var cook = Levels(1, (CombatSkill.Cooking, 999));
            var meals = new SkillBuild();
            Assert.Null(meals.Raise("steady-hands", cook, 100));
            Assert.Null(meals.Raise("thrifty", cook, 100));
            Assert.Contains("Gem Cutter needs Crafting 300.", meals.CanRaise("gem-cutter", cook, 100));
            Assert.Contains("Gem Salvager needs Crafting 300.", meals.CanRaise("gem-salvager", cook, 100));
            Assert.Contains("Enchanter's Patience needs Crafting 600.", meals.CanRaise("enchanters-patience", cook, 100));
            Assert.Null(meals.Raise("master-artisan", cook, 100)); // from Thrifty, past the Crafting nodes
            Assert.Equal("Crafting", SkillTrees.GateName(SkillTrees.Get("gem-cutter")));
            Assert.Null(SkillTrees.Get("thrifty").Skill);
        }

        [Fact]
        public void Gathering_nodes_add_up_as_the_design_says()
        {
            Func<CombatSkill, int> all = skill => 999;
            var build = new SkillBuild();
            Assert.True(TradeSkills.Gathering(build, 999).IsEmpty);
            Assert.True(TradeSkills.Gathering(null, 999).IsEmpty);
            Assert.Equal(1, TradeSkills.Gathering(build, 999).VillagePerRound);
            foreach (var id in new[] { "sure-hands", "villages-share", "bountiful", "tireless", "quick-return" })
                for (var i = 0; i < 5; ++i) Assert.Null(build.Raise(id, all, 400));
            Assert.Null(build.Raise("nest-finder", all, 400));
            var full = TradeSkills.Gathering(build, 998);
            Assert.Equal(0.10, full.FindChance, 6);     // Sure Hands: what 20 levels give
            Assert.Equal(0.30, full.VillageShare, 6);
            Assert.Equal(0.10, full.DoubleFind, 6);
            Assert.Equal(0.10, full.DoubleRound, 6);
            Assert.Equal(0.10, full.WaitCut, 6);
            Assert.True(full.Eggs);
            Assert.False(full.VillageTwice);
            // the capstone, in a skill at 999 only: two for the village a round, Tireless twice as often
            Assert.Null(build.Raise("master-of-the-trade", all, 400));
            Assert.Equal(70, build.Spent);
            Assert.False(TradeSkills.Gathering(build, 998).VillageTwice);
            Assert.Equal(0.10, TradeSkills.Gathering(build, 998).DoubleRound, 6);
            var master = TradeSkills.Gathering(build, 999);
            Assert.True(master.VillageTwice);
            Assert.Equal(2, master.VillagePerRound);
            Assert.Equal(0.20, master.DoubleRound, 6);
            Assert.Equal(0.10, master.DoubleFind, 6); // the rarest finds are left alone by it

            // a find at your own level: 20% becomes 30%
            Assert.Equal(0.30, TradeSkills.FindChance(0.20, full), 6);
            Assert.Equal(0.20, TradeSkills.FindChance(0.20, default(GatherBonus)), 6);
            // waits: Copper Ore's 10 s become 9, Atlarus Ore's six hours 5 h 24 min; no wait stays none
            Assert.Equal(9, TradeSkills.FindWait(10, full), 6);
            Assert.Equal(6 * 3600 * 0.9, TradeSkills.FindWait(6 * 3600, full), 6);
            Assert.Equal(0, TradeSkills.FindWait(0, full), 6);
            Assert.Equal(250, TradeSkills.FindWait(250, default(GatherBonus)), 6);
            // the rarest finds with both nodes: 1.1 / 0.9 = +22%, as the design's table says
            Assert.Equal(1.222, (1 + full.DoubleFind) / (1 - full.WaitCut), 3);
            // about one egg in 20 hours of rounds
            Assert.Equal(20.0, 1 / TradeSkills.EggChancePerRound * 10 / 3600, 6);
        }

        [Fact]
        public void Nest_Finder_gives_the_eggs_of_pets_that_gather()
        {
            var table = PetEggs.Table(EggSource.Gathering, 0).ToList();
            Assert.Equal(new[] { "chicken", "cat", "pig" }, table.Select(t => t.Species.Id).ToArray());
            Assert.All(table, t => Assert.Equal(PetRole.Gatherer, t.Species.Role));
            Assert.All(table, t => Assert.True(t.Species.Hatches));
            // every hatched kind that gathers is in it
            Assert.Equal(PetEggs.Species.Count(s => s.Role == PetRole.Gatherer), table.Count);
            Assert.Equal("chicken", PetEggs.Pick(EggSource.Gathering, 0, 0).Id);
            Assert.Equal("cat", PetEggs.Pick(EggSource.Gathering, 0, 0.5).Id);
            Assert.Equal("pig", PetEggs.Pick(EggSource.Gathering, 0, 0.99).Id);
            // its own table: nothing widened, nothing borrowed from the quests'
            Assert.Equal(1.0, PetEggs.Widened(EggSource.Gathering, 0), 6);
            Assert.All(table, t => Assert.Contains("Nest Finder", t.Species.EggFrom));
        }

        [Fact]
        public void Artisan_nodes_add_up_as_the_design_says()
        {
            Func<CombatSkill, int> all = skill => 999;
            var build = new SkillBuild();
            Assert.True(TradeSkills.Making(build, CombatSkill.Cooking, 999).IsEmpty);
            Assert.True(TradeSkills.Making(null, CombatSkill.Cooking, 999).IsEmpty);
            foreach (var id in new[] { "steady-hands", "thrifty", "second-helping" })
                for (var i = 0; i < 5; ++i) Assert.Null(build.Raise(id, all, 400));
            var cooking = TradeSkills.Making(build, CombatSkill.Cooking, 998);
            Assert.Equal(0.15, cooking.Success, 6);
            Assert.Equal(0.10, cooking.KeepIngredients, 6);
            Assert.Equal(0.10, cooking.ExtraPortion, 6);
            // Second Helping is for meals and potions, not for crafted gear
            Assert.Equal(0.10, TradeSkills.Making(build, CombatSkill.Alchemy, 500).ExtraPortion, 6);
            Assert.Equal(0, TradeSkills.Making(build, CombatSkill.Crafting, 500).ExtraPortion, 6);
            Assert.Equal(0.10, TradeSkills.Making(build, CombatSkill.Crafting, 500).KeepIngredients, 6);
            // below a node's level in the skill used, it does not count: Thrifty (100) at Cooking 99
            Assert.Equal(0, TradeSkills.Making(build, CombatSkill.Cooking, 99).KeepIngredients, 6);
            Assert.Equal(0.15, TradeSkills.Making(build, CombatSkill.Cooking, 99).Success, 6);
            // a recipe that starts at 20% starts at 35%, and nothing goes past certain
            Assert.Equal(0.35, TradeSkills.SuccessChance(0.20, cooking), 6);
            Assert.Equal(1.0, TradeSkills.SuccessChance(0.95, cooking), 6);
            Assert.Equal(0.5, TradeSkills.SuccessChance(0.5, default(MakeBonus)), 6);

            // the capstone: 5% more for each of the two, in a skill at 999, and only where they are learned
            Assert.Null(build.Raise("master-artisan", all, 400));
            Assert.Equal(0.10, TradeSkills.Making(build, CombatSkill.Cooking, 998).KeepIngredients, 6);
            var master = TradeSkills.Making(build, CombatSkill.Cooking, 999);
            Assert.Equal(0.15, master.KeepIngredients, 6);
            Assert.Equal(0.15, master.ExtraPortion, 6);
            Assert.Equal(0.15, master.Success, 6);
            Assert.Equal(0.15, TradeSkills.Making(build, CombatSkill.Crafting, 999).KeepIngredients, 6);
            Assert.Equal(0, TradeSkills.Making(build, CombatSkill.Crafting, 999).ExtraPortion, 6);
            var alone = new SkillBuild();
            alone.Ranks["master-artisan"] = 1;
            Assert.True(TradeSkills.Making(alone, CombatSkill.Cooking, 999).IsEmpty);

            // Gem Cutter: the four fees become 700 / 3,500 / 17,500 / 87,500 at five ranks
            Assert.Equal(1000, TradeSkills.CombineFee(1000, build));
            for (var i = 0; i < 5; ++i) Assert.Null(build.Raise("gem-cutter", all, 400));
            Assert.Equal(0.30, TradeSkills.CombineDiscount(build), 6);
            Assert.Equal(new long[] { 700, 3500, 17500, 87500 }, Gems.CombineFee.Select(fee => TradeSkills.CombineFee(fee, build)).ToArray());
            var two = new SkillBuild();
            two.Ranks["gem-cutter"] = 2;
            Assert.Equal(880, TradeSkills.CombineFee(1000, two));
            Assert.Equal(1, TradeSkills.CombineFee(1, build));
            Assert.Equal(1000, TradeSkills.CombineFee(1000, null));

            // Enchanter's Patience: 15% off the wait at five ranks
            Assert.Equal(1.0, TradeSkills.EnchantWaitFactor(build), 6);
            Assert.Equal(1.0, TradeSkills.EnchantWaitFactor(null), 6);
            for (var i = 0; i < 5; ++i) Assert.Null(build.Raise("enchanters-patience", all, 400));
            Assert.Equal(0.85, TradeSkills.EnchantWaitFactor(build), 6);
            Assert.Equal(84 - 9, build.Spent); // everything but Gem Salvager
        }

        [Fact]
        public void Gem_Salvager_moved_to_Artisan_and_a_build_that_had_it_keeps_it()
        {
            var node = SkillTrees.Get("gem-salvager");
            Assert.Equal(SkillTree.Artisan, node.Tree);
            Assert.Equal(CombatSkill.Crafting, node.Skill);
            Assert.Equal(300, node.RequiredLevel);
            Assert.Equal(3, node.MaxRank);
            Assert.Equal(3, node.Cost);
            Assert.DoesNotContain(SkillTrees.Of(SkillTree.Hunter), n => n.Id == "gem-salvager");
            Assert.Equal("5% better chance to keep a gem you take out a rank", node.EffectText());

            // a build from when it was Hunter's (learned with Slayer 300, after Treasure Hunter): it loads, the ranks
            // and their points are there, and the stat the server reads is what it was
            var old = SkillBuild.Parse("big-game:1;gem-salvager:2;treasure-hunter:1|");
            Assert.Equal(2, old.Rank("gem-salvager"));
            Assert.Equal(1 + 6 + 2, old.Spent);
            Assert.Equal(0.10, old.Passives()[GearStat.GemKeepChance], 6);
            Assert.Equal(old.ToString(), SkillBuild.Parse(old.ToString()).ToString());
            // the rank it holds stays whatever the new path says; one more needs Crafting 300 now
            var slayer = Levels(1, (CombatSkill.Slayer, 999));
            Assert.Contains("Gem Salvager needs Crafting 300.", old.CanRaise("gem-salvager", slayer, 100));
            var crafter = Levels(1, (CombatSkill.Crafting, 300));
            Assert.Null(old.Raise("gem-salvager", crafter, 100));
            Assert.Equal(0.15, old.Passives()[GearStat.GemKeepChance], 6);
            // nothing in Hunter hangs from it, so it can be taken back without the tree minding
            Assert.Null(old.CanLower("gem-salvager"));
            // new to it: after Thrifty or Second Helping
            Assert.Contains("needs Thrifty or Second Helping first", new SkillBuild().CanRaise("gem-salvager", crafter, 100));
        }

        [Fact]
        public void Trade_nodes_say_their_numbers()
        {
            Assert.Equal("2% more chance on every find a rank", SkillTrees.Get("sure-hands").EffectText());
            Assert.Equal("4% now, 10% at 5/5", SkillTrees.Get("sure-hands").TotalText(2));
            Assert.Equal("10% at 5/5", SkillTrees.Get("sure-hands").TotalText(0));
            Assert.Equal("6% chance that a round gives the village one more a rank", SkillTrees.Get("villages-share").EffectText());
            Assert.Equal("30% at 5/5", SkillTrees.Get("villages-share").TotalText(0));
            Assert.Equal("2% chance that a find is two a rank", SkillTrees.Get("bountiful").EffectText());
            Assert.Equal("2% chance that a round counts twice a rank", SkillTrees.Get("tireless").EffectText());
            Assert.Equal("every find's wait is 2% shorter a rank", SkillTrees.Get("quick-return").EffectText());
            Assert.StartsWith("about one egg in 20 hours of gathering", SkillTrees.Get("nest-finder").EffectText());
            Assert.Equal("", SkillTrees.Get("nest-finder").TotalText(1));
            Assert.StartsWith("in a skill at 999: every round counts twice for the village", SkillTrees.Get("master-of-the-trade").EffectText());
            Assert.Equal("3% more chance of success where a recipe can fail a rank", SkillTrees.Get("steady-hands").EffectText());
            Assert.Equal("15% at 5/5", SkillTrees.Get("steady-hands").TotalText(0));
            Assert.Equal("2% chance that making something uses up nothing a rank", SkillTrees.Get("thrifty").EffectText());
            Assert.Equal("2% chance of one more meal or potion a rank", SkillTrees.Get("second-helping").EffectText());
            Assert.Equal("combining gems costs 6% less a rank", SkillTrees.Get("gem-cutter").EffectText());
            Assert.Equal("12% now, 30% at 5/5", SkillTrees.Get("gem-cutter").TotalText(2));
            Assert.Equal("the wait between two enchants is 3% shorter a rank", SkillTrees.Get("enchanters-patience").EffectText());
            Assert.Equal("in a skill at 999: Thrifty and Second Helping are each worth 5% more", SkillTrees.Get("master-artisan").EffectText());
            // no trade node touches a fight: nothing for the game's sheet but Gem Salvager's own stat
            var build = new SkillBuild();
            foreach (var n in SkillTrees.Trees(trade: true).SelectMany(SkillTrees.Of)) build.Ranks[n.Id] = n.MaxRank;
            var stats = build.Passives(petOut: true);
            for (var i = 0; i < GearStats.Count; ++i)
                if ((GearStat)i != GearStat.GemKeepChance) Assert.Equal(0, stats.Values[i]);
            Assert.Empty(build.Actives());
            Assert.Equal(154, build.Spent);
        }
    }
}
