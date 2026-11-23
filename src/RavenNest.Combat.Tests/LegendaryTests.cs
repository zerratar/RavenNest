using System;
using System.Collections.Generic;
using System.Linq;
using RavenNest.Combat;
using Xunit;
using Xunit.Abstractions;

namespace RavenNest.Combat.Tests
{
    /// <summary>The Legendary tier (the game's docs/legendary-design.md): who may find one, the odds and bad-luck count, ranks, the numbers and the powers.</summary>
    public class LegendaryTests
    {
        private readonly ITestOutputHelper output;
        public LegendaryTests(ITestOutputHelper output) => this.output = output;

        private sealed class Fixed : ICombatRandom
        {
            private readonly double value;
            public Fixed(double value) => this.value = value;
            public double Next() => value;
        }

        [Fact]
        public void The_five_are_the_dark_fortress_pieces_with_their_fixed_ids()
        {
            Assert.Equal(5, Legendary.Count);
            Assert.Equal(new[] { "soulreaver", "hellhorn-mace", "batwing-staff", "spiked-bulwark", "fortress-hand-cannon" }, Legendary.All.Select(x => x.Key).ToArray());
            foreach (var l in Legendary.All)
            {
                Assert.True(Legendary.IsLegendary(l.ItemId));
                Assert.Same(l, Legendary.Get(l.ItemId));
                Assert.Same(l, Legendary.GetByKey(l.Key));
                Assert.Equal(Legendary.ItemId(l.Key), l.ItemId);
            }
            Assert.False(Legendary.IsLegendary(Legendary.ItemId("skullcrusher")));
            Assert.Equal(5, Legendary.All.Select(x => x.Power).Distinct().Count());
        }

        [Fact]
        public void A_character_qualifies_only_with_all_four_rules()
        {
            Assert.True(Legendary.Qualifies(950, 21, true, false));
            Assert.True(Legendary.Qualifies(999, 30, true, false));
            Assert.False(Legendary.Qualifies(949, 30, true, false)); // the skill
            Assert.False(Legendary.Qualifies(999, 20, true, false)); // the top band
            Assert.False(Legendary.Qualifies(999, 30, false, false)); // the ending
            Assert.False(Legendary.Qualifies(999, 30, true, true)); // owns it
        }

        [Fact]
        public void Only_the_top_band_and_heroic_dungeons_count()
        {
            Assert.False(Legendary.TowerCounts(20));
            Assert.True(Legendary.TowerCounts(21));
            Assert.True(Legendary.TowerCounts(30));
            Assert.False(Legendary.DungeonCounts(3));
            Assert.True(Legendary.DungeonCounts(4));
            Assert.True(Legendary.DungeonCounts(5));
            // the top band is built: floor 21 is a floor of the tower's last band
            Assert.True(Tower.TopFloor >= Legendary.TowerFloor);
            Assert.Equal(3, Tower.BandOf(Legendary.TowerFloor).Number);
        }

        [Fact]
        public void Bad_luck_raises_the_chance_up_to_ten_times()
        {
            Assert.Equal(0.0025, Legendary.Chance(LegendarySource.Dungeon, 0), 9);
            Assert.Equal(0.005, Legendary.Chance(LegendarySource.Dungeon, 75), 9);
            Assert.Equal(0.001 * 5, Legendary.Chance(LegendarySource.Raid, 300), 9);
            Assert.Equal(0.025, Legendary.Chance(LegendarySource.Dungeon, 675), 9);
            Assert.Equal(0.025, Legendary.Chance(LegendarySource.Dungeon, 100000), 9);
            Assert.Equal(0.4, Legendary.Chance(LegendarySource.Tower, 5000), 9);
            Assert.True(Legendary.Found(LegendarySource.Dungeon, 0, 0.0024));
            Assert.False(Legendary.Found(LegendarySource.Dungeon, 0, 0.0025));
        }

        /// <summary>
        ///     The design's promise (section 3): a dedicated player (about 4 hours a day: 3 heroic dungeons and 8 raids)
        ///     finds a first Legendary in weeks, not days or years; a casual one (1 heroic, 2 raids) in a few months.
        /// </summary>
        [Fact]
        public void Dedicated_player_finds_one_in_weeks()
        {
            (double Mean, int Median, int P10, int P90, int P99) Days(int heroic, int raids, int seed)
            {
                var rng = new Random(seed);
                var days = new List<int>();
                for (var player = 0; player < 20000; ++player)
                {
                    var kills = 0;
                    var day = 0;
                    var found = false;
                    while (!found && day < 2000)
                    {
                        day++;
                        var sources = Enumerable.Repeat(LegendarySource.Dungeon, heroic).Concat(Enumerable.Repeat(LegendarySource.Raid, raids)).OrderBy(_ => rng.Next()).ToList();
                        foreach (var s in sources)
                        {
                            if (Legendary.Found(s, kills, rng.NextDouble())) { found = true; break; }
                            kills++;
                        }
                    }
                    days.Add(day);
                }
                days.Sort();
                return (days.Average(), days[days.Count / 2], days[days.Count / 10], days[days.Count * 9 / 10], days[days.Count * 99 / 100]);
            }

            var dedicated = Days(3, 8, 1);
            var casual = Days(1, 2, 2);
            output.WriteLine($"dedicated: mean {dedicated.Mean:0.0} days, median {dedicated.Median}, 10% {dedicated.P10}, 90% {dedicated.P90}, 99% {dedicated.P99}");
            output.WriteLine($"casual: mean {casual.Mean:0.0} days, median {casual.Median}, 10% {casual.P10}, 90% {casual.P90}, 99% {casual.P99}");
            Assert.InRange(dedicated.Mean, 14, 35); // weeks
            Assert.True(dedicated.P10 >= 3); // not days for most
            Assert.True(dedicated.P99 <= 90); // never years
            Assert.InRange(casual.Mean, 45, 120);
        }

        [Fact]
        public void The_hellhorn_mace_is_found_by_strength_and_worn_with_attack()
        {
            var mace = Legendary.GetByKey("hellhorn-mace");
            Assert.Equal(LegendarySkill.Attack, mace.Skill);
            Assert.Equal(LegendarySkill.Strength, mace.FindSkill);
            Assert.Equal(SkillTree.Might, mace.Tree);
            Assert.Contains(SkillTrees.Capstones(SkillTree.Might), n => n.Id == "colossus");
            // no two Legendaries share a finding skill and tree any more
            Assert.Equal(Legendary.Count, Legendary.All.Select(x => (x.FindSkill, x.Tree)).Distinct().Count());
            var soulreaver = Legendary.GetByKey("soulreaver");
            Assert.Equal(LegendarySkill.Attack, soulreaver.FindSkill);
        }

        [Fact]
        public void Legends_echo_awakens_the_least_awakened_then_gives_a_gem()
        {
            Assert.Equal(1, Legendary.EchoTarget(new[] { 120, 30, 400 }));
            Assert.Equal(0, Legendary.EchoTarget(new[] { 10, 10 })); // the first on a tie
            Assert.Equal(1, Legendary.EchoTarget(new[] { 400, 399 }));
            Assert.Equal(-1, Legendary.EchoTarget(new[] { 400, 999 })); // every one at rank 5: a gem
            Assert.Equal(-1, Legendary.EchoTarget(new int[0]));
            Assert.Equal(10, Legendary.EchoKills);
            Assert.Equal(GemTier.Flawless, Legendary.EchoGemTier);
        }

        /// <summary>
        ///     A dedicated player who qualifies for all five, over two years: five finds, then Legend's Echoes (rare: the
        ///     find odds with the bad-luck count) that awaken the least awakened one, then Flawless gems.
        /// </summary>
        [Fact]
        public void A_dedicated_player_finds_all_five_then_echoes_now_and_then()
        {
            var rng = new Random(7);
            var allFive = new List<int>();
            var echoes = new List<int>();
            for (var player = 0; player < 2000; ++player)
            {
                int owned = 0, pity = 0, echo = 0, day = 0, fifth = -1;
                for (day = 1; day <= 730; ++day)
                {
                    var sources = Enumerable.Repeat(LegendarySource.Dungeon, 3).Concat(Enumerable.Repeat(LegendarySource.Raid, 8));
                    foreach (var s in sources)
                    {
                        if (!Legendary.Found(s, pity, rng.NextDouble())) { pity++; continue; }
                        pity = 0;
                        if (owned < Legendary.Count) { owned++; if (owned == Legendary.Count) fifth = day; }
                        else echo++;
                    }
                }
                if (fifth > 0) { allFive.Add(fifth); echoes.Add(echo); }
            }
            allFive.Sort();
            var median = allFive[allFive.Count / 2];
            var perMonth = echoes.Sum() / (double)allFive.Zip(echoes, (f, e) => 730 - f).Sum() * 30;
            output.WriteLine($"all five: median day {median} ({allFive.Count} of 2000 within two years); echoes after that: {perMonth:0.00} a month");
            Assert.InRange(median, 70, 160); // about four months for all five
            Assert.InRange(perMonth, 0.8, 2.5); // rare: one or two a month
        }

        [Fact]
        public void Ranks_come_from_boss_kills_worn()
        {
            Assert.Equal(1, Legendary.RankOf(0));
            Assert.Equal(1, Legendary.RankOf(29));
            Assert.Equal(2, Legendary.RankOf(30));
            Assert.Equal(3, Legendary.RankOf(90));
            Assert.Equal(4, Legendary.RankOf(200));
            Assert.Equal(5, Legendary.RankOf(400));
            Assert.Equal(5, Legendary.RankOf(99999));
            Assert.Equal(30, Legendary.NextRankKills(0));
            Assert.Equal(90, Legendary.NextRankKills(37));
            Assert.Equal(0, Legendary.NextRankKills(400));
            Assert.Equal("legend:37", Legendary.Tag(37));
            Assert.Equal(37, Legendary.KillsOf("legend:37"));
            Assert.Equal(0, Legendary.KillsOf(null));
            Assert.Equal(0, Legendary.KillsOf("look:1;hull=1"));
            Assert.Equal(3, Legendary.RankOfTag("legend:90"));
            Assert.Equal("Rank 2 of 5: 37 of 90 boss kills to rank 3", Legendary.DescribeAwakening(37));
            Assert.Equal("Rank 5 of 5: fully awakened", Legendary.DescribeAwakening(400));
        }

        [Fact]
        public void Higher_ranks_open_sockets_and_the_glow()
        {
            Assert.Equal(new[] { 3, 3, 4, 4, 5 }, Enumerable.Range(1, 5).Select(Legendary.Sockets).ToArray());
            Assert.Equal(0f, Legendary.Glow(1));
            Assert.Equal(0f, Legendary.Glow(2));
            Assert.True(Legendary.Glow(3) > 0 && Legendary.Glow(4) > Legendary.Glow(3) && Legendary.Glow(5) == 1f);
            Assert.True(Gems.Sockets(Legendary.RequiredLevel, GemSlot.Weapon) <= Legendary.Sockets(1)); // never fewer than a normal item of its level
        }

        [Fact]
        public void A_legendary_stays_above_the_best_by_its_margin()
        {
            // the Elder Atlarus 2H Sword, the best 2H sword on 2026-11-23: 776 / 776
            Assert.Equal(854, Legendary.Derive(776));
            Assert.Equal(854, Legendary.AtRank(854, 1));
            Assert.Equal(932, Legendary.AtRank(854, 5)); // 854 x 1.20 / 1.10
            Assert.Equal(0.10, Legendary.Margin(1), 9);
            Assert.Equal(0.20, Legendary.Margin(5), 9);
            for (var best = 1; best < 3000; best += 7)
            {
                var r1 = Legendary.Derive(best);
                Assert.True(r1 > best, "rank 1 beats the best " + best);
                var last = r1;
                for (var rank = 2; rank <= 5; ++rank)
                {
                    var v = Legendary.AtRank(r1, rank);
                    Assert.True(v >= last, "rank " + rank + " never below the rank before");
                    last = v;
                }
            }
        }

        [Fact]
        public void Every_power_grows_with_the_rank()
        {
            foreach (var p in Enum.GetValues(typeof(LegendaryPower)).Cast<LegendaryPower>())
            {
                var one = Legendary.Values(p, 1);
                var five = Legendary.Values(p, 5);
                Assert.False(string.IsNullOrEmpty(Legendary.Describe(p, 1)));
                Assert.DoesNotContain("—", Legendary.Describe(p, 3));
                Assert.DoesNotContain("–", Legendary.Describe(p, 3));
                switch (p)
                {
                    case LegendaryPower.SoulHarvest: Assert.True(five.KillHeal > one.KillHeal); break;
                    case LegendaryPower.Dread: Assert.True(five.FearChance > one.FearChance && five.FearSeconds > one.FearSeconds); break;
                    case LegendaryPower.BatSwarm: Assert.True(five.SwarmShare > one.SwarmShare && five.SwarmEvery < one.SwarmEvery && five.SwarmBites == 3); break;
                    case LegendaryPower.IronAegis: Assert.True(five.Thorns > one.Thorns && five.ShieldShare > one.ShieldShare); break;
                    case LegendaryPower.ThunderShot: Assert.True(five.BlastBonus > one.BlastBonus && five.BlastSplash > one.BlastSplash); break;
                }
            }
        }

        [Fact]
        public void Iron_aegis_thorns_answer_every_style()
        {
            var attacker = new CombatSheet { CombatLevel = 100, MaxHealth = 100000, DefenseLevel = 1, CritChance = 0 };
            attacker.Ranged.DamageLevel = 100;
            attacker.Ranged.AimLevel = 1000;
            var plain = new CombatSheet { CombatLevel = 100, MaxHealth = 100000, DefenseLevel = 1 };
            var bulwark = new CombatSheet { CombatLevel = 100, MaxHealth = 100000, DefenseLevel = 1 };
            GearStats.Parse("THORNS:8%").ApplyTo(plain, null);
            GearStats.Parse("THORNS:8%").ApplyTo(bulwark, null);
            Legendary.Stats(LegendaryPower.IronAegis, 1).ApplyTo(bulwark, null);
            Assert.Equal(0, CombatMath.Attack(attacker, AttackStyle.Ranged, plain, new Fixed(0.01)).Reflected);
            var shot = CombatMath.Attack(attacker, AttackStyle.Ranged, bulwark, new Fixed(0.01));
            Assert.Equal((int)Math.Round(shot.Total * 0.13), shot.Reflected); // 8% + Iron Aegis's 5%
        }

        [Fact]
        public void Swarm_waits_and_the_third_shot_blasts()
        {
            var state = new LegendaryState();
            var v = Legendary.Values(LegendaryPower.BatSwarm, 1);
            Assert.Equal(350, LegendaryRules.Swarm(state, v, 1000, 0));
            Assert.Equal(0, LegendaryRules.Swarm(state, v, 1000, 9.9));
            Assert.Equal(350, LegendaryRules.Swarm(state, v, 1000, 10));

            var shots = new LegendaryState();
            Assert.False(LegendaryRules.BlastNext(shots));
            Assert.False(LegendaryRules.ShotHit(shots));
            Assert.False(LegendaryRules.ShotHit(shots));
            Assert.True(LegendaryRules.BlastNext(shots));
            Assert.True(LegendaryRules.ShotHit(shots));
            Assert.False(LegendaryRules.BlastNext(shots));

            var aegis = new LegendaryState();
            Assert.True(LegendaryRules.Aegis(aegis, 0));
            Assert.False(LegendaryRules.Aegis(aegis, 9.9));
            Assert.True(LegendaryRules.Aegis(aegis, 10));

            Assert.False(LegendaryRules.Feared(Legendary.Values(LegendaryPower.Dread, 5), true, 0.0)); // bosses shrug it off
            Assert.True(LegendaryRules.Feared(Legendary.Values(LegendaryPower.Dread, 1), false, 0.079));
            Assert.False(LegendaryRules.Feared(Legendary.Values(LegendaryPower.Dread, 1), false, 0.08));
            Assert.Equal(500, LegendaryRules.KillHeal(Legendary.Values(LegendaryPower.SoulHarvest, 1), 10000));
        }
    }
}
