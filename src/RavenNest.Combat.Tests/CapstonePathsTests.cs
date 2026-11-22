using RavenNest.Combat;
using System;
using System.Linq;
using Xunit;

namespace RavenNest.Combat.Tests
{
    /// <summary>
    ///     The capstone paths (the game's docs/capstone-paths-design.md): a combat-side tree ends in three capstones of
    ///     which a character learns one, and what each of the 18 new ones does.
    /// </summary>
    public class CapstonePathsTests
    {
        private static int All(CombatSkill skill) => 999;

        /// <summary>Always the same roll: 0.3 hits, never crits at the base chance, blocks at 50%.</summary>
        private sealed class Fixed : ICombatRandom
        {
            private readonly double value;
            public Fixed(double value) => this.value = value;
            public double Next() => value;
        }

        private static CombatSheet Fighter(string stats = null, int level = 500)
        {
            var sheet = new CombatSheet { CombatLevel = level, MaxHealth = 5000, DefenseLevel = level, Armor = 400, IsPlayer = true };
            foreach (var s in new[] { sheet.Melee, sheet.Ranged, sheet.Magic, sheet.Healing })
            {
                s.DamageLevel = level;
                s.AimLevel = level * 4; // hits whatever the roll
                s.Power = 300;
                s.Accuracy = 300;
            }
            if (stats != null) GearStats.Parse(stats).ApplyExtras(sheet, null);
            return sheet;
        }

        private static CombatSheet Enemy(int armor = 400) => new CombatSheet { CombatLevel = 500, MaxHealth = 100000, DefenseLevel = 100, Armor = armor };

        private static SkillBuild With(params string[] ids)
        {
            var build = new SkillBuild();
            foreach (var id in ids) build.Ranks[id] = 1;
            return build;
        }

        // ---- the rule ----

        [Fact]
        public void A_combat_tree_ends_in_three_capstones_and_a_trade_tree_in_one()
        {
            foreach (var tree in SkillTrees.Trees(trade: false))
            {
                var three = SkillTrees.Capstones(tree);
                Assert.Equal(3, three.Count);
                Assert.All(three, c =>
                {
                    Assert.True(c.Capstone);
                    Assert.Equal(999, c.RequiredLevel);
                    Assert.Equal(SkillTrees.CapstoneCost, c.Cost);
                    Assert.Equal(1, c.MaxRank);
                    Assert.False(string.IsNullOrEmpty(c.Path));
                    Assert.False(c.IsActive);
                    Assert.False(string.IsNullOrEmpty(c.EffectText()) && !c.PetTwin, c.Id);
                });
                // today's is the first and asks for one of its level 600 nodes, as it did; the new two ask for all of
                // the nodes of their theme, which are in their own tree and below them
                Assert.False(three[0].NeedsAll);
                foreach (var c in three.Skip(1))
                {
                    Assert.True(c.NeedsAll, c.Id);
                    Assert.NotEmpty(c.Requires);
                    Assert.All(c.Requires, r => { var n = SkillTrees.Get(r); Assert.NotNull(n); Assert.Equal(tree, n.Tree); Assert.False(n.Capstone); });
                    Assert.Null(GearStats.Parse(c.Passive).Unknown);
                    Assert.False(GearStats.Parse(c.Passive).IsEmpty, c.Id);
                }
            }
            foreach (var tree in SkillTrees.Trees(trade: true)) Assert.Single(SkillTrees.Capstones(tree));
            Assert.Equal(27, SkillTrees.All.Count(n => n.Capstone && !SkillTrees.IsTrade(n.Tree)));
            // paths and capstones have names of their own: no two alike, and none is another skill's name
            var names = SkillTrees.All.Select(n => n.Name).Concat(SkillTrees.All.Where(n => n.Capstone && n.Path != n.Name).Select(n => n.Path)).ToList();
            Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public void Todays_capstones_are_unchanged()
        {
            (string id, string stats, string[] path)[] today =
            {
                ("unbreakable", "DAMAGE_REDUCTION:6%;THREAT:25%", new[] { "rally", "last-stand" }),
                ("miracle-worker", "HEALING_POWER_BONUS:15%;HEAL_SHIELD:10%", new[] { "sanctuary", "smite" }),
                ("colossus", "MELEE_POWER_BONUS:12%;EXECUTE_DAMAGE:10%", new[] { "leap", "titan" }),
                ("perfect-form", "CRIT_CHANCE:4%;CRIT_DAMAGE:20%", new[] { "disarm", "precision-strikes" }),
                ("deadeye", "RANGED_POWER_BONUS:12%;ARMOR_PENETRATION:15%", new[] { "snare", "rapid-fire" }),
                ("archmage", "MAGIC_POWER_BONUS:12%;CAST_SPEED:8%", new[] { "meteor" }),
                ("undying", "MAX_HEALTH_BONUS:10%;COMBAT_REGEN:0.6%", new[] { "iron-skin" }),
                ("legend-of-the-hunt", "BOSS_DAMAGE:10%;LOOT_LUCK:5%", new[] { "apex-predator" }),
            };
            foreach (var (id, stats, path) in today)
            {
                var node = SkillTrees.Get(id);
                Assert.Equal(stats, node.Passive);
                Assert.Equal(path, node.Requires);
                Assert.Same(node, SkillTrees.Capstones(node.Tree)[0]);
            }
            Assert.True(SkillTrees.Get("pack-leader").PetTwin);
            // a build saved before the paths loads as it was
            var old = SkillBuild.Parse("apex-predator:1;big-game:5;legend-of-the-hunt:1;pressure-points:5;treasure-hunter:5|");
            Assert.True(old.Has("legend-of-the-hunt"));
            Assert.Equal("legend-of-the-hunt", old.CapstoneOf(SkillTree.Hunter).Id);
            Assert.Equal(0.05 + 5 * 0.02, old.Passives()[GearStat.LootLuck], 6);
        }

        [Fact]
        public void Only_one_of_a_trees_three_can_be_learned_and_the_reply_names_the_one_chosen()
        {
            var build = With("sharp-edge", "deadly", "swift", "flurry", "precision-strikes");
            Assert.Null(build.CapstoneOf(SkillTree.Precision));
            Assert.Null(build.CanRaise("first-blood", All, 300));
            Assert.Null(build.CanRaise("perfect-form", All, 300));
            Assert.Null(build.Raise("first-blood", All, 300));
            Assert.Equal("first-blood", build.CapstoneOf(SkillTree.Precision).Id);

            var refused = build.CanRaise("perfect-form", All, 300);
            Assert.Contains("You chose First Blood", refused);
            Assert.Contains("Precision", refused);
            Assert.Contains("respec", refused);
            Assert.NotNull(build.Raise("flowing-steel", All, 300));
            Assert.False(build.Has("perfect-form"));
            Assert.False(build.Has("flowing-steel"));
            // another tree's ending is its own choice
            var other = With("iron-skin");
            other.Ranks["first-blood"] = 1;
            Assert.Null(other.CanRaise("undying", All, 300));

            // taking it back frees the choice (the planner's way; a respec empties the whole build)
            Assert.Null(build.CanLower("first-blood"));
            build.Lower("first-blood");
            Assert.Null(build.CapstoneOf(SkillTree.Precision));
            Assert.Null(build.Raise("flowing-steel", All, 300));
            build.Reset();
            Assert.Null(build.CapstoneOf(SkillTree.Precision));
        }

        [Fact]
        public void A_new_capstone_asks_for_every_node_of_its_theme()
        {
            var build = With("keen-eye", "sharp-edge", "deadly", "swift");
            // First Blood: Sharp Edge and Precision Strikes; the reply names what is missing
            Assert.Equal("First Blood needs Precision Strikes first.", build.CanRaise("first-blood", All, 300));
            Assert.Equal("Flowing Steel needs Flurry first.", build.CanRaise("flowing-steel", All, 300));
            Assert.Equal("Shield of the Many needs Rally and Thick Skin first.", new SkillBuild().CanRaise("shield-of-the-many", All, 300));
            Assert.Equal("Sharp Edge and Precision Strikes", SkillTrees.PathText(SkillTrees.Get("first-blood")));
            Assert.Equal("Disarm or Precision Strikes", SkillTrees.PathText(SkillTrees.Get("perfect-form")));
            build.Ranks["precision-strikes"] = 1;
            Assert.Null(build.Raise("first-blood", All, 300));
            // and holds every one of them up: none can be taken back from under it
            Assert.Contains("First Blood needs Sharp Edge for its path", build.CanLower("sharp-edge"));
            Assert.Contains("First Blood needs Precision Strikes for its path", build.CanLower("precision-strikes"));
            // the level still gates it
            Assert.Contains("needs Attack 999", With("sharp-edge", "precision-strikes").CanRaise("first-blood", s => 998, 300));
            Assert.Contains("costs 15 points", With("sharp-edge", "precision-strikes").CanRaise("first-blood", All, 14));
        }

        [Fact]
        public void A_build_or_link_with_two_capstones_of_one_tree_keeps_the_first()
        {
            var two = SkillBuild.Parse("flowing-steel:1;first-blood:1;perfect-form:1;undying:1;crimson-pact:1|");
            Assert.True(two.Has("flowing-steel"));
            Assert.False(two.Has("first-blood"));
            Assert.False(two.Has("perfect-form"));
            Assert.True(two.Has("undying"));
            Assert.False(two.Has("crimson-pact"));
            Assert.Equal(30, two.Spent);
            // and reads back the same after saving
            Assert.Equal(two.ToString(), SkillBuild.Parse(two.ToString()).ToString());
        }

        [Fact]
        public void The_trees_cost_one_capstone_each()
        {
            var nodes = SkillTrees.All.Where(n => !n.Capstone).Sum(n => n.Cost * n.MaxRank);
            Assert.Equal(nodes + 11 * SkillTrees.CapstoneCost, SkillTrees.TotalCost);
            Assert.Equal(957, SkillTrees.TotalCost); // as before the paths
        }

        [Fact]
        public void A_capstone_is_found_by_its_name_or_its_paths()
        {
            Assert.Equal("first-blood", SkillTrees.Find("first blood").Id);
            Assert.Equal("first-blood", SkillTrees.Find("assassin").Id);
            Assert.Equal("first-blood", SkillTrees.Find("Assassin").Id);
            Assert.Equal("perfect-form", SkillTrees.Find("duelist").Id);
            Assert.Equal("flowing-steel", SkillTrees.Find("blade dancer").Id);
            Assert.Equal("blood-rage", SkillTrees.Find("berserker").Id);
            Assert.Equal("aftershock", SkillTrees.Find("earthshaker").Id);
            Assert.Equal("holy-fire", SkillTrees.Find("crusader").Id);
            Assert.Equal("guardian-angel", SkillTrees.Find("lifebinder").Id);
            Assert.Equal("clean-kill", SkillTrees.Find("sniper").Id);
            Assert.Equal("volley", SkillTrees.Find("stormbow").Id);
            Assert.Equal("wildfire", SkillTrees.Find("pyromancer").Id);
            Assert.Equal("eye-of-the-storm", SkillTrees.Find("stormcaller").Id);
            Assert.Equal("crimson-pact", SkillTrees.Find("blood drinker").Id);
            Assert.Equal("thin-the-herd", SkillTrees.Find("culler").Id);
            Assert.Equal("retribution", SkillTrees.Find("avenger").Id);
            Assert.Equal("fortunes-favorite", SkillTrees.Find("fortune").Id);
            Assert.Equal("alpha", SkillTrees.Find("alpha").Id);
            Assert.Equal("trailblazer", SkillTrees.Find("trail").Id);
            // a skill's own name wins over a path's: Colossus, Undying and the like are both
            Assert.Equal("colossus", SkillTrees.Find("colossus").Id);
            // "guardian" could be the Guardian's ending, Guardian's Oath or Guardian Angel: nothing is learned on a guess
            Assert.Null(SkillTrees.Find("guardian"));
            Assert.Contains("could mean", SkillTrees.NotFoundText("guardian"));
            Assert.Equal("shield-of-the-many", SkillTrees.Find("shield of the many").Id);
        }

        // ---- what the 18 do ----

        [Fact]
        public void First_Blood_the_first_hit_on_a_new_target_crits_through_part_of_its_armor_and_crits_poison()
        {
            var assassin = Fighter(SkillTrees.Get("first-blood").Passive);
            Assert.Equal(0.25, assassin.FirstStrike, 6);
            Assert.Equal(0.04, assassin.PoisonOnCrit, 6);
            var plain = Fighter();
            var state = new PathState();
            object a = "a", b = "b";
            var roll = new Fixed(0.3);

            var first = PathRules.Before(state, assassin, a, AttackOptions.Basic, true, now: 100);
            Assert.True(first.Critical);
            Assert.Equal(0.25, first.ArmorIgnored, 6);
            var hit = CombatMath.Attack(assassin, AttackStyle.Melee, Enemy(), roll, first);
            Assert.True(hit.Critical);
            Assert.True(PathRules.After(state, assassin, AttackStyle.Melee, a, hit, true, now: 100).First);
            // harder than the same roll without it: the crit, and a quarter of the armor
            var normal = CombatMath.Attack(plain, AttackStyle.Melee, Enemy(), roll);
            Assert.False(normal.Critical);
            var lessArmor = (1 - CombatMath.ArmorReduction(300)) / (1 - CombatMath.ArmorReduction(400));
            Assert.InRange(hit.Total / (double)normal.Total, 1.5 * lessArmor - 0.02, 1.5 * lessArmor + 0.02);
            // the crit poisons: 4% of the hit a second
            Assert.Equal(hit.Total * 0.04, hit.Poison, 6);
            Assert.Equal(0, normal.Poison);

            // the second hit on the same target is a plain one
            Assert.False(PathRules.Before(state, assassin, a, AttackOptions.Basic, true, now: 101.5).Critical);
            Assert.False(PathRules.After(state, assassin, AttackStyle.Melee, a, normal, true, now: 101.5).First);
            // another target is new, but the first hit is ready only twelve seconds after the last one landed
            Assert.False(PathRules.Before(state, assassin, b, AttackOptions.Basic, true, now: 105).Critical);
            Assert.False(PathRules.Before(state, assassin, b, AttackOptions.Basic, true, now: 111.9).Critical);
            Assert.True(PathRules.Before(state, assassin, b, AttackOptions.Basic, true, now: 112).Critical);
            // a miss keeps it for the blow that lands
            Assert.False(PathRules.After(state, assassin, AttackStyle.Melee, b, new AttackResult { Hit = false }, true, now: 112).First);
            Assert.True(PathRules.Before(state, assassin, b, AttackOptions.Basic, true, now: 113.5).Critical);
            Assert.True(PathRules.After(state, assassin, AttackStyle.Melee, b, hit, true, now: 113.5).First);
            Assert.False(PathRules.Before(state, assassin, a, AttackOptions.Basic, true, now: 115).Critical);
            // a skill's blow is never the first hit, and does not use it up
            var fresh = new PathState();
            Assert.False(PathRules.Before(fresh, assassin, a, AttackOptions.Basic, false, now: 50).Critical);
            Assert.False(PathRules.After(fresh, assassin, AttackStyle.Melee, a, hit, false, now: 50).First);
            Assert.True(PathRules.Before(fresh, assassin, a, AttackOptions.Basic, true, now: 51).Critical);

            // the target fell: the same enemy come back is a new target
            var again = new PathState();
            PathRules.Before(again, assassin, a, AttackOptions.Basic, true, now: 0);
            PathRules.After(again, assassin, AttackStyle.Melee, a, hit, true, now: 0);
            again.Forget(a);
            Assert.True(PathRules.Before(again, assassin, a, AttackOptions.Basic, true, now: 20).Critical);
            // and so is it after six seconds without a blow struck (a miss or a skill's blow counts as one)
            var rested = new PathState();
            PathRules.Before(rested, assassin, a, AttackOptions.Basic, true, now: 0);
            PathRules.After(rested, assassin, AttackStyle.Melee, a, hit, true, now: 0);
            PathRules.After(rested, assassin, AttackStyle.Melee, a, new AttackResult { Hit = false }, true, now: 10);
            PathRules.Rest(rested, 15);
            Assert.False(PathRules.Before(rested, assassin, a, AttackOptions.Basic, true, now: 15).Critical);
            PathRules.Rest(rested, 16.5);
            Assert.True(PathRules.Before(rested, assassin, a, AttackOptions.Basic, true, now: 16.5).Critical);
            // without the capstone nothing of this happens
            Assert.False(PathRules.Before(new PathState(), plain, a, AttackOptions.Basic, true, now: 100).Critical);
        }

        [Fact]
        public void A_crits_poison_does_not_stack()
        {
            var set = new StatusEffectSet();
            set.Venom(10, PathRules.PoisonSeconds);
            set.Venom(6, PathRules.PoisonSeconds);
            Assert.Equal(1, set.Find(StatusEffectKind.Poison).Stacks);
            Assert.Equal(10, set.Find(StatusEffectKind.Poison).DamagePerSecond, 6);
            Assert.Equal(10, set.Tick(1).Poison, 6);
            set.Tick(3);
            Assert.False(set.Has(StatusEffectKind.Poison));
            // on a target with element poison already: its stacks stay, the stronger damage counts
            set.Poison(4);
            set.Poison(4);
            set.Venom(9, 4);
            Assert.Equal(2, set.Find(StatusEffectKind.Poison).Stacks);
            Assert.Equal(9, set.Find(StatusEffectKind.Poison).DamagePerSecond, 6);
        }

        [Fact]
        public void Flowing_Steel_every_hit_in_a_row_is_faster_up_to_five_and_a_new_target_starts_again()
        {
            var dancer = Fighter(SkillTrees.Get("flowing-steel").Passive);
            Assert.Equal(1.05, dancer.AttackSpeed, 6);
            Assert.Equal(0.01, dancer.ComboSpeed, 6);
            var state = new PathState();
            object a = "a", b = "b";
            var hit = new AttackResult { Hit = true, Total = 100 };
            Assert.Equal(0, PathRules.ComboSpeed(state, dancer), 6);
            for (var i = 1; i <= 8; ++i)
            {
                PathRules.After(state, dancer, AttackStyle.Melee, a, hit, true);
                Assert.Equal(0.01 * Math.Min(i, 5), PathRules.ComboSpeed(state, dancer), 6);
            }
            Assert.Equal(0.05, PathRules.ComboSpeed(state, 0.01), 6);
            // a miss does not break it, a skill's blow neither adds nor breaks, another target starts at one
            PathRules.After(state, dancer, AttackStyle.Melee, a, new AttackResult { Hit = false }, true);
            PathRules.After(state, dancer, AttackStyle.Melee, b, hit, false);
            Assert.Equal(0.05, PathRules.ComboSpeed(state, dancer), 6);
            PathRules.After(state, dancer, AttackStyle.Melee, b, hit, true);
            Assert.Equal(0.01, PathRules.ComboSpeed(state, dancer), 6);
            Assert.Equal(0, PathRules.ComboSpeed(state, Fighter()), 6);
        }

        [Fact]
        public void Blood_Rage_hits_harder_the_less_health_is_left_and_steals_life()
        {
            Assert.Equal(0, PathRules.Rage(1), 6);
            Assert.Equal(0.5, PathRules.Rage(0.65), 6);
            Assert.Equal(1, PathRules.Rage(0.3), 6);
            Assert.Equal(1, PathRules.Rage(0.05), 6);

            var berserker = Fighter(SkillTrees.Get("blood-rage").Passive);
            Assert.Equal(0.25, berserker.RagePower, 6);
            Assert.Equal(0.03, berserker.LifeSteal, 6);
            var roll = new Fixed(0.3);
            var full = CombatMath.Attack(berserker, AttackStyle.Melee, Enemy(), roll).Total;
            berserker.CurrentHealthShare = 0.65;
            var half = CombatMath.Attack(berserker, AttackStyle.Melee, Enemy(), roll).Total;
            berserker.CurrentHealthShare = 0.2;
            var low = CombatMath.Attack(berserker, AttackStyle.Melee, Enemy(), roll);
            Assert.InRange(half / (double)full, 1.115, 1.135);
            Assert.InRange(low.Total / (double)full, 1.24, 1.26);
            Assert.Equal((int)Math.Round(low.Total * 0.03), low.Healed);
            // melee only
            var shot = CombatMath.Attack(berserker, AttackStyle.Ranged, Enemy(), roll).Total;
            berserker.CurrentHealthShare = 1;
            Assert.Equal(CombatMath.Attack(berserker, AttackStyle.Ranged, Enemy(), roll).Total, shot);
        }

        [Fact]
        public void Aftershock_every_fourth_melee_hit_strikes_the_others_for_its_share()
        {
            var shaker = Fighter(SkillTrees.Get("aftershock").Passive);
            Assert.Equal(0.7, shaker.Aftershock, 6);
            Assert.Equal(0.2, shaker.CrowdControlDuration, 6);
            var state = new PathState();
            object a = "a", b = "b";
            var hit = new AttackResult { Hit = true, Total = 800 };
            var splashes = Enumerable.Range(1, 12).Select(i => PathRules.After(state, shaker, AttackStyle.Melee, i % 2 == 0 ? a : b, hit, true).Splash).ToList();
            Assert.Equal(new[] { 0, 0, 0, 560, 0, 0, 0, 560, 0, 0, 0, 560 }, splashes);
            // misses, skills' blows, shots and spells do not count
            var other = new PathState();
            for (var i = 0; i < 8; ++i)
            {
                Assert.Equal(0, PathRules.After(other, shaker, AttackStyle.Melee, a, new AttackResult { Hit = false }, true).Splash);
                Assert.Equal(0, PathRules.After(other, shaker, AttackStyle.Melee, a, hit, false).Splash);
                Assert.Equal(0, PathRules.After(other, shaker, AttackStyle.Ranged, a, hit, true).Splash);
            }
            Assert.Equal(0, other.MeleeHits);
            Assert.Equal(0, Enumerable.Range(0, 8).Sum(_ => PathRules.After(new PathState(), Fighter(), AttackStyle.Melee, a, hit, true).Splash));
        }

        [Fact]
        public void Volley_every_third_shot_hits_a_second_enemy_for_its_share()
        {
            var bow = Fighter(SkillTrees.Get("volley").Passive);
            Assert.Equal(1.0, bow.Volley, 6);
            Assert.Equal(1.10, bow.AttackSpeed, 6);
            var state = new PathState();
            var hit = new AttackResult { Hit = true, Total = 500 };
            var seconds = Enumerable.Range(1, 9).Select(_ => PathRules.After(state, bow, AttackStyle.Ranged, "a", hit, true).Second).ToList();
            Assert.Equal(new[] { 0, 0, 500, 0, 0, 500, 0, 0, 500 }, seconds);
            Assert.Equal(0, PathRules.After(new PathState { Shots = 2 }, bow, AttackStyle.Melee, "a", hit, true).Second);
        }

        [Fact]
        public void Shield_of_the_Many_takes_a_share_of_an_allys_blow_after_the_guardians_armor()
        {
            var guardian = Fighter(SkillTrees.Get("shield-of-the-many").Passive);
            Assert.Equal(0.15, guardian.AllyGuard, 6);
            Assert.Equal(150, PathRules.GuardShare(1000, guardian.AllyGuard));
            Assert.Equal(0, PathRules.GuardShare(1000, 0));
            Assert.Equal(0, PathRules.GuardShare(1, 0.15)); // never the whole blow
            Assert.Equal(500, PathRules.GuardShare(1000, 0.9)); // nor more than half, whatever gear adds
            // the guardian's armor and damage reduction take their cut of the share
            guardian.Armor = 640;
            Assert.Equal(75, PathRules.GuardTaken(150, guardian));
            guardian.DamageTakenMultiplier = 0.8;
            Assert.Equal(60, PathRules.GuardTaken(150, guardian));
            Assert.Equal(0, PathRules.GuardTaken(0, guardian));
            // only while the guardian has more than 30% of its health: the game and the simulator ask before sharing
            Assert.Equal(0.3, PathRules.GuardFloor, 6);
        }

        [Fact]
        public void Retribution_the_hit_after_a_block_deals_more_and_only_that_one()
        {
            var avenger = Fighter(SkillTrees.Get("retribution").Passive + ";BLOCK_CHANCE:50%");
            Assert.Equal(0.10, avenger.Thorns, 6);
            Assert.Equal(0.60, avenger.BlockRetort, 6);
            var state = new PathState();
            var roll = new Fixed(0.3);
            // an enemy's blow is blocked: half of it, a tenth of it back, and the next hit is marked
            var blow = CombatMath.Attack(Enemy(), AttackStyle.Melee, avenger, roll, new AttackOptions { CannotMiss = true });
            Assert.True(blow.Blocked);
            Assert.Equal((int)Math.Round(blow.Total * 0.10), blow.Reflected);
            PathRules.Blocked(state, avenger);
            Assert.True(state.Retort);

            var plain = CombatMath.Attack(avenger, AttackStyle.Melee, Enemy(), roll).Total;
            var options = PathRules.Before(state, avenger, "a", AttackOptions.Basic, true);
            Assert.Equal(1.6, options.Multiplier, 6);
            var answer = CombatMath.Attack(avenger, AttackStyle.Melee, Enemy(), roll, options);
            Assert.InRange(answer.Total / (double)plain, 1.59, 1.61);
            Assert.True(PathRules.After(state, avenger, AttackStyle.Melee, "a", answer, true).Retorted);
            Assert.False(state.Retort);
            Assert.Equal(1, PathRules.Before(state, avenger, "a", AttackOptions.Basic, true).Multiplier, 6);
            // a miss keeps the answer for the blow that lands; a skill's blow can be the answer (its own multiplier counts too)
            PathRules.Blocked(state, avenger);
            PathRules.After(state, avenger, AttackStyle.Melee, "a", new AttackResult { Hit = false }, true);
            Assert.True(state.Retort);
            Assert.Equal(4.8, PathRules.Before(state, avenger, "a", new AttackOptions { Multiplier = 3 }, false).Multiplier, 6);
            // without the capstone a block marks nothing
            var none = new PathState();
            PathRules.Blocked(none, Fighter("BLOCK_CHANCE:50%"));
            Assert.False(none.Retort);
        }

        [Fact]
        public void Holy_Fire_a_heal_strikes_for_its_share_and_harder_on_the_undead()
        {
            var crusader = Fighter(SkillTrees.Get("holy-fire").Passive);
            Assert.Equal(0.20, crusader.HealStrike, 6);
            Assert.Equal(0.20, crusader.UndeadDamage, 6);
            Assert.Equal(200, PathRules.HealStrike(1000, crusader, Enemy(armor: 0)));
            // armor takes half its usual cut, as of an element: 640 armor halves a blow and takes a quarter of this
            Assert.Equal(150, PathRules.HealStrike(1000, crusader, Enemy(armor: 640)));
            var undead = Enemy(armor: 0);
            undead.Family = EnemyFamily.Undead;
            Assert.Equal(240, PathRules.HealStrike(1000, crusader, undead));
            undead.DamageTakenMultiplier = 1.2; // Mark Prey
            Assert.Equal(288, PathRules.HealStrike(1000, crusader, undead));
            Assert.Equal(0, PathRules.HealStrike(1000, Fighter(), Enemy()));
            Assert.Equal(0, PathRules.HealStrike(0, crusader, Enemy()));
            Assert.Equal(1, PathRules.HealStrike(1, crusader, Enemy()));
        }

        [Fact]
        public void Guardian_Angel_saves_one_ally_a_minute_and_heals_a_share_of_their_health()
        {
            var monk = Fighter(SkillTrees.Get("guardian-angel").Passive);
            Assert.Equal(0.40, monk.GuardianAngel, 6);
            Assert.Equal(1.10, monk.CastSpeed, 6);
            Assert.Equal(4000, PathRules.AngelHeal(10000, monk.GuardianAngel));
            Assert.Equal(0, PathRules.AngelHeal(10000, 0));
            var state = new PathState();
            Assert.True(PathRules.UseAngel(state, 100));
            Assert.False(PathRules.UseAngel(state, 130));
            Assert.False(PathRules.UseAngel(state, 159.9));
            Assert.True(PathRules.UseAngel(state, 160));
            Assert.True(PathRules.UseAngel(new PathState(), 0)); // ready from the start
        }

        [Fact]
        public void Clean_Kill_hits_the_healthy_harder()
        {
            var sniper = Fighter(SkillTrees.Get("clean-kill").Passive);
            Assert.Equal(0.50, sniper.HealthyDamage, 6);
            Assert.Equal(CombatMath.BaseCritDamage + 0.30, sniper.CritDamage, 6);
            var roll = new Fixed(0.3);
            var enemy = Enemy();
            var healthy = CombatMath.Attack(sniper, AttackStyle.Ranged, enemy, roll).Total;
            enemy.CurrentHealthShare = 0.81;
            Assert.Equal(healthy, CombatMath.Attack(sniper, AttackStyle.Ranged, enemy, roll).Total);
            enemy.CurrentHealthShare = 0.8;
            var hurt = CombatMath.Attack(sniper, AttackStyle.Ranged, enemy, roll).Total;
            Assert.InRange(healthy / (double)hurt, 1.49, 1.51);
            Assert.Equal(hurt, CombatMath.Attack(Fighter(), AttackStyle.Ranged, Enemy(), roll).Total);
        }

        [Fact]
        public void Wildfire_burns_harder_and_a_dead_enemys_burn_moves_on()
        {
            var pyro = Fighter(SkillTrees.Get("wildfire").Passive);
            Assert.Equal(1.50, pyro.BurnBonus, 6);
            Assert.True(pyro.BurnSpread > 0);
            var dead = new StatusEffectSet();
            dead.Burn(50, ElementRules.BurnSeconds);
            dead.Tick(3);
            var burn = dead.TakeBurn();
            Assert.NotNull(burn);
            Assert.False(dead.Has(StatusEffectKind.Burn));
            Assert.Null(dead.TakeBurn());
            Assert.Equal(50 * pyro.BurnSpread, PathRules.BurnJump(burn.DamagePerSecond, pyro.BurnSpread), 6);
            Assert.Equal(0, PathRules.BurnJump(50, 0), 6);
            Assert.Equal(50, PathRules.BurnJump(50, 3), 6); // never stronger than it was
        }

        [Fact]
        public void Eye_of_the_Storm_is_two_stats_the_game_has()
        {
            var storm = Fighter(SkillTrees.Get("eye-of-the-storm").Passive);
            Assert.Equal(0.30, storm.LightningJumpBonus, 6);
            Assert.Equal(0.10, storm.CooldownReduction, 6);
        }

        [Fact]
        public void Crimson_Pact_life_steal_beyond_full_health_becomes_a_shield_up_to_its_share()
        {
            var drinker = Fighter(SkillTrees.Get("crimson-pact").Passive);
            Assert.Equal(0.05, drinker.LifeSteal, 6);
            Assert.Equal(0.15, drinker.LifeStealShield, 6);
            // 300 healed with 100 missing: 200 over, into the shield
            Assert.Equal(200, PathRules.OverflowShield(300, 100, 0, 10000, 0.10));
            Assert.Equal(700, PathRules.OverflowShield(300, 0, 400, 10000, 0.10));
            Assert.Equal(1000, PathRules.OverflowShield(300, 0, 900, 10000, 0.10)); // 10% of health at most
            Assert.Equal(1000, PathRules.OverflowShield(300, 0, 1000, 10000, 0.10));
            Assert.Equal(2500, PathRules.OverflowShield(300, 0, 2500, 10000, 0.10)); // a Barrier's bigger shield stays
            Assert.Equal(0, PathRules.OverflowShield(300, 300, 0, 10000, 0.10)); // all of it fit into health
            Assert.Equal(50, PathRules.OverflowShield(300, 500, 50, 10000, 0.10));
            Assert.Equal(0, PathRules.OverflowShield(300, 0, 0, 10000, 0)); // without the capstone
        }

        [Fact]
        public void Ironblood_fewer_crits_taken_and_more_healing_received()
        {
            var iron = Fighter(SkillTrees.Get("ironblood").Passive);
            Assert.True(iron.CritResistance >= 0.10);
            Assert.Equal(0.35, iron.HealingReceived, 6);
            var enemy = Enemy();
            enemy.Melee.DamageLevel = 500; enemy.Melee.AimLevel = 2000; enemy.Melee.Power = 300;
            enemy.CritChance = 0.15;
            var random = new SeededCombatRandom(5);
            var onIron = Enumerable.Range(0, 4000).Count(_ => CombatMath.Attack(enemy, AttackStyle.Melee, iron, random).Critical);
            var onPlain = Enumerable.Range(0, 4000).Count(_ => CombatMath.Attack(enemy, AttackStyle.Melee, Fighter(), random).Critical);
            Assert.InRange(onPlain, 500, 700);
            Assert.InRange(onIron, 120, 280);
        }

        [Fact]
        public void Thin_the_Herd_hits_everything_but_bosses_harder()
        {
            var culler = Fighter(SkillTrees.Get("thin-the-herd").Passive);
            Assert.Equal(0.08, culler.NonBossDamage, 6);
            Assert.Equal(0.10, culler.KillRush, 6);
            var roll = new Fixed(0.3);
            var plain = CombatMath.Attack(Fighter(), AttackStyle.Melee, Enemy(), roll).Total;
            Assert.InRange(CombatMath.Attack(culler, AttackStyle.Melee, Enemy(), roll).Total / (double)plain, 1.075, 1.085);
            var boss = Enemy();
            boss.IsBoss = true;
            Assert.Equal(plain, CombatMath.Attack(culler, AttackStyle.Melee, boss, roll).Total);
            // the rush after a kill is a timed "damage dealt" change like Call of the Wild's: the game adds it
            culler.DamageMultiplier *= 1 + culler.KillRush;
            Assert.InRange(CombatMath.Attack(culler, AttackStyle.Melee, Enemy(), roll).Total / (double)plain, 1.18, 1.195);
        }

        [Fact]
        public void Fortunes_Favorite_is_loot_luck_and_nothing_else()
        {
            var stats = GearStats.Parse(SkillTrees.Get("fortunes-favorite").Passive);
            Assert.Equal(0.12, stats[GearStat.LootLuck], 6);
            Assert.Equal(1, stats.Values.Count(v => v != 0));
        }

        [Fact]
        public void Loot_luck_is_that_share_more_items_also_where_the_item_was_sure()
        {
            // a raid's item: half the time, 22% more often with Treasure Hunter's five ranks and Fortune's Favorite
            var build = With("treasure-hunter", "fortunes-favorite");
            build.Ranks["treasure-hunter"] = 5;
            Assert.Equal(0.22, LootLuck.Of(build), 6);
            LootLuck.Chances(0.5, 0.22, out var first, out var second);
            Assert.Equal(0.61, first, 6);
            Assert.Equal(0, second, 6);
            // a dungeon's sure item: still sure, and a 22% chance of one more
            LootLuck.Chances(1, 0.22, out first, out second);
            Assert.Equal(1, first, 6);
            Assert.Equal(0.22, second, 6);
            LootLuck.Chances(0.8, 0.5, out first, out second);
            Assert.Equal(1, first, 6);
            Assert.Equal(0.2, second, 6);
            // without luck nothing changes, and no stack of it doubles the loot
            LootLuck.Chances(0.8, 0, out first, out second);
            Assert.Equal(0.8, first, 6);
            Assert.Equal(0, second, 6);
            LootLuck.Chances(1, 3, out first, out second);
            Assert.Equal(LootLuck.Max, second, 6);
            Assert.Equal(0, LootLuck.Of(new SkillBuild()), 6);
            Assert.Equal(0, LootLuck.Items(0.5, 0.22, 0.62, 0));
            Assert.Equal(1, LootLuck.Items(0.5, 0.22, 0.60, 0));
            Assert.Equal(2, LootLuck.Items(1, 0.22, 0.99, 0.21));
            Assert.Equal(1, LootLuck.Items(1, 0.22, 0.99, 0.23));
            // on average: the share more
            var random = new SeededCombatRandom(11);
            var plain = Enumerable.Range(0, 40000).Sum(_ => LootLuck.Items(0.9, 0, random.Next(), random.Next()));
            var lucky = Enumerable.Range(0, 40000).Sum(_ => LootLuck.Items(0.9, 0.22, random.Next(), random.Next()));
            Assert.InRange(lucky / (double)plain, 1.20, 1.24);
        }

        [Fact]
        public void Alpha_one_bigger_stronger_pet_and_Trailblazer_a_better_gatherer()
        {
            var alpha = With("fang-and-claw", "guardians-oath", "alpha");
            var stats = alpha.Passives();
            Assert.Equal(0.04 + 0.40, stats[GearStat.PetPower], 6);
            Assert.Equal(0.40, stats[GearStat.PetHealth], 6);
            Assert.True(stats[GearStat.PetAlpha] > 0);
            Assert.False(alpha.HasPetTwin);
            // a tree has one ending: no packmate beside the Alpha
            Assert.Contains("You chose Alpha", alpha.CanRaise("pack-leader", All, 300));

            var trail = With("keen-senses", "back-on-its-feet", "trailblazer");
            trail.Ranks["keen-senses"] = 3;
            var t = trail.Passives();
            // half as strong again, on top of Keen Senses's three ranks: the server counts 1 + this (BattlePetManager)
            Assert.Equal(0.30 + 0.50, t[GearStat.PetGathering], 6);
            Assert.True(t[GearStat.PetSafeGathering] > 0);
        }

        [Fact]
        public void Every_new_stat_has_words_for_chat_and_the_website()
        {
            foreach (var node in SkillTrees.All.Where(n => n.Capstone && n.NeedsAll))
            {
                var text = node.EffectText();
                Assert.False(string.IsNullOrWhiteSpace(text), node.Id);
                // plain words, not "+30% Rage Power"
                Assert.DoesNotMatch(@"[+-]\d", text);
                Assert.DoesNotContain("—", text);
                Assert.DoesNotContain("–", text);
                Assert.DoesNotContain("armour", text);
            }
            Assert.Equal("3% of the damage you deal heals you, up to 25% more melee damage as your health falls (all of it at 30% health)", SkillTrees.Get("blood-rage").EffectText());
        }
    }
}
