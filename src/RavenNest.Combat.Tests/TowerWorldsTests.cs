using System;
using System.Collections.Generic;
using System.Linq;
using RavenNest.Combat;
using Xunit;

namespace RavenNest.Combat.Tests
{
    /// <summary>
    ///     The tower as worlds (the game's docs/tower-worlds-proposal.md section 12): floors and parts over the server's
    ///     plain steps, a part's objectives by kind, and scaling by pressure.
    /// </summary>
    public class TowerWorldsTests
    {
        // ---- floors and parts ------------------------------------------------------------------------------------

        [Fact]
        public void EveryStepIsAPartOfOneFloorAndMapsBack()
        {
            var seen = 0;
            for (var floor = 1; floor <= Tower.FloorCount; ++floor)
            {
                var parts = Tower.PartsIn(floor);
                Assert.True(parts > 0);
                for (var part = 1; part <= parts; ++part)
                {
                    var step = Tower.StepOf(floor, part);
                    Assert.True(step > 0);
                    Assert.Equal(floor, Tower.FloorOf(step));
                    Assert.Equal(part, Tower.PartOf(step));
                    Assert.NotNull(Tower.Get(step));
                    Assert.Equal(floor, Tower.Get(step).Floor);
                    Assert.Equal(part, Tower.Get(step).Part);
                    seen++;
                }
                Assert.Equal(Tower.StepOf(floor, 1), Tower.FirstStep(floor));
                Assert.Equal(Tower.StepOf(floor, parts), Tower.LastStep(floor));
            }
            // the steps run through the floors without a gap
            Assert.Equal(Tower.TopFloor, seen);
            for (var step = 1; step <= Tower.TopFloor; ++step) Assert.Equal(step, Tower.Get(step).Number);
        }

        [Theory]
        [InlineData(1, 1, 1)]
        [InlineData(7, 1, 7)]
        [InlineData(10, 1, 10)]
        [InlineData(11, 2, 1)]
        [InlineData(14, 2, 4)]
        [InlineData(21, 3, 1)]
        [InlineData(30, 3, 10)]
        public void TodaysStepsMapStraightAcross(int step, int floor, int part)
        {
            Assert.Equal(floor, Tower.FloorOf(step));
            Assert.Equal(part, Tower.PartOf(step));
            Assert.Equal(step, Tower.StepOf(floor, part));
            Assert.Equal("Floor " + floor + ", part " + part, Tower.Label(step));
            Assert.Equal(floor + "-" + part, Tower.Short(step));
        }

        [Fact]
        public void WhatDoesNotExistIsZeroOrEmpty()
        {
            Assert.Equal(0, Tower.FloorOf(0));
            Assert.Equal(0, Tower.FloorOf(Tower.TopFloor + 1));
            Assert.Equal(0, Tower.PartOf(Tower.TopFloor + 1));
            Assert.Equal(0, Tower.StepOf(1, 0));
            Assert.Equal(0, Tower.StepOf(1, Tower.PartsIn(1) + 1));
            Assert.Equal(0, Tower.StepOf(Tower.FloorCount + 1, 1));
            Assert.Equal(0, Tower.PartsIn(0));
            Assert.Equal("", Tower.Short(0));
            // a stream that has cleared everything stands past the top: its label is the top's
            Assert.Equal(Tower.Label(Tower.TopFloor), Tower.Label(Tower.TopFloor + 1));
        }

        [Fact]
        public void TheLastPartOfAFloorIsItsFloorBossAndEveryOtherAMiniBoss()
        {
            foreach (var band in Tower.Bands)
            {
                for (var step = band.FirstFloor; step <= band.LastFloor; ++step)
                    Assert.Equal(step == band.LastFloor, Tower.IsFloorBoss(step));
            }
            Assert.True(Tower.Get(10).IsFloorBoss);
            Assert.True(Tower.Get(20).IsFloorBoss);
            Assert.True(Tower.Get(30).IsFloorBoss);
            Assert.False(Tower.Get(9).IsFloorBoss);
            Assert.False(Tower.IsFloorBoss(0));
            Assert.False(Tower.IsFloorBoss(Tower.TopFloor + 1));
        }

        [Fact]
        public void TheLatestPartOfAFloorIsTheStreamsOwnOrTheFloorsLast()
        {
            Assert.Equal(14, Tower.LatestStep(2, 14)); // its own floor: its part
            Assert.Equal(10, Tower.LatestStep(1, 14)); // a floor below: its last part
            Assert.Equal(0, Tower.LatestStep(3, 14));  // a floor above: not reached
            Assert.Equal(30, Tower.LatestStep(3, 31)); // everything cleared: the top
            Assert.Equal(0, Tower.FloorsCleared(9));
            Assert.Equal(1, Tower.FloorsCleared(10));
            Assert.Equal(1, Tower.FloorsCleared(19));
            Assert.Equal(3, Tower.FloorsCleared(30));
        }

        [Theory]
        [InlineData("2-4", 2, 4)]
        [InlineData("2 4", 2, 4)]
        [InlineData("floor 2 part 4", 2, 4)]
        [InlineData("Floor 2, Part 4", 2, 4)]
        [InlineData("floor 2 4", 2, 4)]
        [InlineData("part 4 floor 2", 2, 4)]
        [InlineData("floor 2", 2, 0)]
        [InlineData("part 7", 0, 7)]
        [InlineData("7", 0, 7)]
        public void PlacesAreReadTheWaysChatTypesThem(string text, int floor, int part)
        {
            Assert.True(Tower.TryParsePlace(text, out var f, out var p));
            Assert.Equal(floor, f);
            Assert.Equal(part, p);
        }

        [Theory]
        [InlineData("")]
        [InlineData("boss")]
        [InlineData("floor")]
        [InlineData("0")]
        [InlineData("1-2-3")]
        [InlineData("2-x")]
        public void WhatIsNotAPlaceIsRefused(string text)
        {
            Assert.False(Tower.TryParsePlace(text, out _, out _));
        }

        // ---- the last hit and the chest --------------------------------------------------------------------------

        [Fact]
        public void TheLastHitGoesToWhoeverStruckAsTheBossFellAndATieIsShared()
        {
            var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid(); var d = Guid.NewGuid();
            var outsider = Guid.NewGuid();
            var group = new[] { a, b, c, d };

            Assert.Equal(new[] { a }, Tower.LastHits(new[] { a }, group));
            // blows that landed together share it, the killing blow first, each once
            Assert.Equal(new[] { b, a }, Tower.LastHits(new[] { b, a, b }, group));
            // someone who was not in the attempt (another session, a made-up id) never gets it, and does not use up a place
            Assert.Equal(new[] { c }, Tower.LastHits(new[] { outsider, Guid.Empty, c }, group));
            // no more than three share one blow
            Assert.Equal(new[] { a, b, c }, Tower.LastHits(new[] { a, b, c, d }, group));
            Assert.Equal(new[] { a }, Tower.LastHits(new[] { a, b, c, d }, group, 1));
            // nobody valid: nobody
            Assert.Empty(Tower.LastHits(null, group));
            Assert.Empty(Tower.LastHits(new Guid[0], group));
            Assert.Empty(Tower.LastHits(new[] { outsider }, group));
            Assert.Empty(Tower.LastHits(new[] { a }, null));
            Assert.Empty(Tower.LastHits(new[] { a }, new Guid[0]));
        }

        [Fact]
        public void AFloorBossAndEveryFifthPartGiveTwoItems()
        {
            Assert.Equal(1, Tower.ChestItems(1));
            Assert.Equal(2, Tower.ChestItems(5));
            Assert.Equal(1, Tower.ChestItems(9));
            Assert.Equal(2, Tower.ChestItems(10));
            Assert.Equal(2, Tower.ChestItems(15));
            Assert.Equal(2, Tower.ChestItems(30));
            Assert.Equal(1, Tower.ChestItems(0));
            foreach (var band in Tower.Bands) Assert.Equal(2, Tower.ChestItems(band.LastFloor));
        }

        // ---- objectives ------------------------------------------------------------------------------------------

        [Fact]
        public void EveryPartHasCountedObjectivesAndItsGuardianLast()
        {
            foreach (var part in Tower.Floors)
            {
                Assert.True(part.Objectives.Length >= 2, part.Label);
                var guardian = part.Objectives[part.Objectives.Length - 1];
                Assert.Equal(TowerObjectiveKind.Guardian, guardian.Kind);
                Assert.Equal(Tower.GuardianId, guardian.Id);
                Assert.Equal(part.Boss.Id, guardian.Target);
                Assert.False(guardian.Counted);
                Assert.Equal(1, part.Objectives.Count(q => q.Kind == TowerObjectiveKind.Guardian));
                Assert.Contains(part.Objectives, q => q.Kind == TowerObjectiveKind.Kill);
                // ids are unique in a part, every text is there, and nothing uses the kind kept for the gates yet
                Assert.Equal(part.Objectives.Length, part.Objectives.Select(q => q.Id).Distinct().Count());
                foreach (var q in part.Objectives)
                {
                    Assert.False(string.IsNullOrWhiteSpace(q.Text), part.Label + " " + q.Id);
                    Assert.True(q.Amount > 0, part.Label + " " + q.Id);
                    Assert.NotEqual(TowerObjectiveKind.GateItem, q.Kind);
                    Assert.DoesNotContain("—", q.Text + q.Story);
                    Assert.DoesNotContain("–", q.Text + q.Story);
                    if (q.Kind == TowerObjectiveKind.Kill) Assert.Contains(part.Enemies, e => e.Id == q.Target);
                    if (q.Kind == TowerObjectiveKind.Gather) Assert.NotEmpty(q.Skills);
                    if (q.Npc != null)
                    {
                        // the one who asks lives in the floor's town
                        var town = Tower.TownFor(part.Number);
                        Assert.Contains(town.People, n => n.Key == q.Npc);
                    }
                }
            }
        }

        [Fact]
        public void EveryFloorHasAFewGatherAndFindParts()
        {
            foreach (var band in Tower.Bands)
            {
                var parts = Tower.Floors.Where(p => p.Floor == band.Number).ToList();
                Assert.True(parts.Count(p => p.Objectives.Any(q => q.Kind == TowerObjectiveKind.Gather)) >= 2, band.Name);
                Assert.True(parts.Count(p => p.Objectives.Any(q => q.Kind == TowerObjectiveKind.Find)) >= 2, band.Name);
                // and some parts stay the simplest case: kills and the guardian
                Assert.Contains(parts, p => p.Objectives.All(q => q.Kind == TowerObjectiveKind.Kill || q.Kind == TowerObjectiveKind.Guardian));
            }
        }

        private static TowerFloorDefinition PartWith(TowerObjectiveKind kind) =>
            Tower.Floors.First(p => p.Objectives.Any(q => q.Kind == kind));

        [Fact]
        public void ProgressKeepsEveryCountedObjectiveAndCapsItsCount()
        {
            var part = PartWith(TowerObjectiveKind.Gather);
            var empty = Tower.ParseProgress(part, null);
            Assert.Equal(part.Objectives.Count(q => q.Counted), empty.Count);
            Assert.DoesNotContain(Tower.GuardianId, empty.Keys);
            Assert.False(Tower.ObjectivesDone(part, empty));

            var kill = part.Objectives.First(q => q.Kind == TowerObjectiveKind.Kill);
            var read = Tower.ParseProgress(part, kill.Id + ":" + (kill.Amount + 500) + ";nonsense:4;guardian:1;broken");
            Assert.Equal(kill.Amount, read[kill.Id]);
            Assert.DoesNotContain("nonsense", read.Keys);
            Assert.DoesNotContain(Tower.GuardianId, read.Keys);

            var full = Tower.ParseProgress(part, string.Join(";", part.Objectives.Where(q => q.Counted).Select(q => q.Id + ":" + q.Amount)));
            Assert.True(Tower.ObjectivesDone(part, full));
            Assert.Equal(full, Tower.ParseProgress(part, Tower.FormatProgress(full)));
        }

        [Fact]
        public void AReportCountsKillsByKindEveryKillForGatheringAndOneFindAtATime()
        {
            var part = Tower.Floors.First(p => p.Objectives.Any(q => q.Kind == TowerObjectiveKind.Gather));
            var kill = part.Objectives.First(q => q.Kind == TowerObjectiveKind.Kill);
            var gather = part.Objectives.First(q => q.Kind == TowerObjectiveKind.Gather);
            var progress = Tower.ParseProgress(part, null);

            Assert.True(Tower.AddReport(part, progress, Tower.ParseCounts(kill.Id + ":3;" + Tower.AllKills + ":7"), 500));
            Assert.Equal(3, progress[kill.Id]);
            Assert.Equal(7, progress[gather.Id]); // what the monsters carried: nobody is stuck without gatherers

            // a report's count is capped, and nothing passes its amount
            Assert.True(Tower.AddReport(part, progress, Tower.ParseCounts(kill.Id + ":100000"), 5));
            Assert.Equal(8, progress[kill.Id]);
            Assert.True(Tower.AddReport(part, progress, Tower.ParseCounts(kill.Id + ":100000"), 100000));
            Assert.Equal(kill.Amount, progress[kill.Id]);
            Assert.False(Tower.AddReport(part, progress, Tower.ParseCounts(kill.Id + ":1"), 500));
            Assert.False(Tower.AddReport(part, progress, Tower.ParseCounts("unknown:5;" + Tower.GuardianId + ":1"), 500));

            var finds = PartWith(TowerObjectiveKind.Find);
            var find = finds.Objectives.First(q => q.Kind == TowerObjectiveKind.Find);
            var found = Tower.ParseProgress(finds, null);
            Assert.True(Tower.AddReport(finds, found, Tower.ParseCounts(find.Id + ":5"), 500));
            Assert.Equal(1, found[find.Id]); // hidden things are found one at a time
            // kills do not find things
            Assert.False(Tower.AddReport(finds, Tower.ParseProgress(finds, find.Id + ":" + find.Amount), Tower.ParseCounts(find.Id + ":1"), 500));
        }

        [Fact]
        public void GatheringCountsOnlyTheSkillsAnObjectiveNames()
        {
            var part = PartWith(TowerObjectiveKind.Gather);
            var gather = part.Objectives.First(q => q.Kind == TowerObjectiveKind.Gather);
            var skill = gather.Skills[0];
            var other = new[] { "Woodcutting", "Mining", "Fishing", "Farming", "Gathering" }.First(s => !part.Objectives.Any(q => q.Skills.Contains(s)));
            Assert.True(Tower.Gathers(part, skill));
            Assert.False(Tower.Gathers(part, other));
            Assert.False(Tower.Gathers(part, "Attack"));
            Assert.False(Tower.Gathers(null, skill));

            var progress = Tower.ParseProgress(part, null);
            Assert.False(Tower.AddGathered(part, progress, other, 5));
            Assert.True(Tower.AddGathered(part, progress, skill, 5));
            Assert.Equal(5, progress[gather.Id]);
            Assert.True(Tower.AddGathered(part, progress, skill, gather.Amount * 2));
            Assert.Equal(gather.Amount, progress[gather.Id]);
            Assert.False(Tower.AddGathered(part, progress, skill, 1));
            // kill objectives never move by gathering
            foreach (var q in part.Objectives.Where(q => q.Kind == TowerObjectiveKind.Kill)) Assert.Equal(0, progress[q.Id]);
        }

        [Fact]
        public void HiddenThingsTakeLongerToFindHigherUp()
        {
            Assert.True(Tower.FindSeconds(Tower.FirstStep(1)) >= 120);
            Assert.True(Tower.FindSeconds(Tower.FirstStep(2)) > Tower.FindSeconds(Tower.FirstStep(1)));
            Assert.True(Tower.FindSeconds(Tower.FirstStep(3)) > Tower.FindSeconds(Tower.FirstStep(2)));
            Assert.InRange(Tower.FindFarShare, 0.05, 1);
        }

        // ---- scaling by pressure ---------------------------------------------------------------------------------

        [Fact]
        public void PressureIsTheGroupAgainstTheSuggestedOneTimesItsStrength()
        {
            Assert.Equal(1.0, TowerPressure.Of(5, 1.0), 9);
            Assert.Equal(0.4, TowerPressure.Of(2, 1.0), 9);
            Assert.Equal(6.0, TowerPressure.Of(30, 1.0), 9);
            Assert.Equal(1.4, TowerPressure.Of(5, 1.4), 9);
            // strength counts between 1 and 1.5: weaker players do not shrink a fight, and nothing counts for more
            Assert.Equal(1.0, TowerPressure.Of(5, 0.3), 9);
            Assert.Equal(1.5, TowerPressure.Of(5, 9.0), 9);
            Assert.Equal(0.0, TowerPressure.Of(0, 1.0), 9);
            Assert.Equal(1.0, TowerPressure.Of(8, 1.0, 8), 9);
        }

        [Fact]
        public void AGroupsPressureComesFromEachPlayersStrength()
        {
            // fresh players: their number against the suggested group
            Assert.Equal(1.0, TowerPressure.Of(new[] { 1.0, 1.0, 1.0, 1.0, 1.0 }), 9);
            Assert.Equal(0.4, TowerPressure.Of(new[] { 1.0, 1.0 }), 9);
            // veterans count for more, up to one and a half
            Assert.Equal(1.4, TowerPressure.Of(new[] { 1.4, 1.4, 1.4, 1.4, 1.4 }), 9);
            Assert.Equal(1.5, TowerPressure.Of(new[] { 3.0, 3.0, 3.0, 3.0, 3.0 }), 9);
            // a newcomer far below the part is a quarter of a head at least, and does not make the fight bigger
            var four = TowerPressure.Of(new[] { 1.0, 1.0, 1.0, 1.0 });
            var withNewcomer = TowerPressure.Of(new[] { 1.0, 1.0, 1.0, 1.0, 0.05 });
            Assert.Equal(0.8, four, 9);
            Assert.Equal(0.85, withNewcomer, 9);
            Assert.True(withNewcomer < TowerPressure.Of(new[] { 1.0, 1.0, 1.0, 1.0, 1.0 }));
            // a mixed group: the heads, times the average of what they have beyond fresh players
            Assert.Equal(4.5 / 5 * 1.1, TowerPressure.Of(new[] { 1.5, 1.0, 1.0, 1.0, 0.5 }), 9);
            Assert.Equal(1.1, TowerPressure.GroupStrength(new[] { 1.5, 1.0, 1.0, 1.0, 0.5 }), 9);
            Assert.Equal(1.0, TowerPressure.GroupStrength(new double[0]), 9);
            Assert.Equal(0.0, TowerPressure.Of(new double[0]), 9);
            Assert.Equal(0.0, TowerPressure.Of((double[])null), 9);
            Assert.Equal(1.0, TowerPressure.Of(new[] { 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0 }, 8), 9);
        }

        [Fact]
        public void TheAmountsSetAPaceThatGrowsWithTheClimb()
        {
            // kills a part asks for: more on every floor, and the first part is the gentlest of all
            int Kills(TowerFloorDefinition p) => p.Objectives.Where(q => q.Kind == TowerObjectiveKind.Kill).Sum(q => q.Amount);
            var first = Kills(Tower.Get(1));
            foreach (var part in Tower.Floors) Assert.True(Kills(part) >= first, part.Label);
            for (var floor = 1; floor < Tower.FloorCount; ++floor)
            {
                var here = Tower.Floors.Where(p => p.Floor == floor).Average(p => (double)Kills(p));
                var above = Tower.Floors.Where(p => p.Floor == floor + 1).Average(p => (double)Kills(p));
                Assert.True(above > here * 1.2, "floor " + (floor + 1));
            }
            // a gather objective is more than the part's kills alone bring in while its kill objectives fill, so the
            // stream's gatherers shorten the part, and no more than twice that, so fighting alone still gets there
            foreach (var part in Tower.Floors)
            {
                foreach (var q in part.Objectives.Where(q => q.Kind == TowerObjectiveKind.Gather))
                {
                    var killsMeanwhile = part.Objectives.Where(k => k.Kind == TowerObjectiveKind.Kill).Max(k => k.Amount) * part.Enemies.Length;
                    Assert.True(q.Amount > killsMeanwhile, part.Label);
                    Assert.True(q.Amount <= killsMeanwhile * 2, part.Label);
                }
            }
        }

        [Fact]
        public void AFullGroupOfFreshPlayersMeetsTheFixedNumbers()
        {
            foreach (var part in Tower.Floors)
            {
                var fixedBoss = Tower.Boss(part);
                var boss = Tower.Boss(part, 1.0);
                Assert.Equal(fixedBoss.Health, boss.Health);
                Assert.Equal(1.0, boss.DamageScale, 9);
                Assert.Equal(Tower.Enemy(part, part.Enemies[0]).Health, Tower.Enemy(part, part.Enemies[0], 1.0).Health);
                Assert.Equal(part.Boss.AddsCount, TowerPressure.Helpers(part.Boss.AddsCount, part.Floor, 1.0));
            }
        }

        [Fact]
        public void AFloorBossIsMeasuredAgainstEightAndAMiniBossAgainstThePartsGroup()
        {
            foreach (var part in Tower.Floors)
                Assert.Equal(part.IsFloorBoss ? Tower.FloorBossGroup : Tower.SuggestedGroup, Tower.BossGroup(part));
            Assert.Equal(8, Tower.FloorBossGroup);
            // eight fresh players meet a floor boss's fixed numbers; five meet a smaller fight, softened helpers too
            var boss = Tower.Get(10);
            Assert.Equal(Tower.Boss(boss).Health, Tower.Boss(boss, TowerPressure.Of(8, 1.0, Tower.BossGroup(boss))).Health);
            var five = TowerPressure.Of(5, 1.0, Tower.BossGroup(boss));
            Assert.Equal(0.625, five, 9);
            Assert.True(Tower.Boss(boss, five).Health < Tower.Boss(boss).Health);
            Assert.True(Tower.Boss(boss, five).DamageScale < 1);
            Assert.Equal(Tower.Boss(boss, five).DamageScale, Tower.Enemy(boss, boss.Enemies[0], five, true).DamageScale, 9);
            // helpers of a full group or a crowd strike as always, and a part's own monsters always do
            Assert.Equal(1.0, Tower.Enemy(boss, boss.Enemies[0], 3.0, true).DamageScale, 9);
            Assert.Equal(1.0, Tower.Enemy(boss, boss.Enemies[0], 0.4).DamageScale, 9);
        }

        [Fact]
        public void NothingGoesBelowTheFixedNumbersForAFullGroupOrMore()
        {
            for (var floor = 1; floor <= 6; ++floor)
            {
                for (var pressure = 1.0; pressure <= 9; pressure += 0.25)
                {
                    Assert.True(TowerPressure.Health(floor, pressure) >= 1);
                    Assert.True(TowerPressure.BossBlows(floor, pressure) >= 1);
                    Assert.True(TowerPressure.BossBlows(floor, pressure) <= 1 + TowerPressure.MaxBlows + 1e-9);
                    Assert.True(TowerPressure.Helpers(3, floor, pressure) >= 3);
                    Assert.True(TowerPressure.RespawnSeconds(floor, pressure) <= Tower.EnemyRespawnSeconds);
                    Assert.True(TowerPressure.RespawnSeconds(floor, pressure) >= Tower.EnemyRespawnSeconds / TowerPressure.MaxRespawnSpeed - 1e-9);
                }
            }
        }

        [Fact]
        public void HowHardScalingBitesGrowsWithTheFloor()
        {
            // thirty fresh players: pressure 6
            Assert.Equal(3.0, TowerPressure.Health(1, 6), 9);  // 40%
            Assert.Equal(4.0, TowerPressure.Health(2, 6), 9);  // 60%
            Assert.Equal(5.0, TowerPressure.Health(3, 6), 9);  // 80%
            Assert.Equal(6.0, TowerPressure.Health(4, 6), 9);  // all of it
            Assert.Equal(6.0, TowerPressure.Health(9, 6), 9);
            for (var floor = 1; floor < 5; ++floor)
            {
                Assert.True(TowerPressure.Bite(floor) <= TowerPressure.Bite(floor + 1));
                Assert.True(TowerPressure.Health(floor, 3) <= TowerPressure.Health(floor + 1, 3));
            }
            Assert.True(TowerPressure.Health(4, 1000) <= TowerPressure.MaxHealthScale);
        }

        [Fact]
        public void SmallGroupsGetMercyThatShrinksWithTheFloor()
        {
            // two players (0.4): toward the group's size, down to the floor's mercy
            Assert.Equal(0.5, TowerPressure.Health(1, 0.4), 9);
            Assert.Equal(TowerPressure.Mercy(2), TowerPressure.Health(2, 0.4), 9);
            Assert.Equal(TowerPressure.Mercy(4), TowerPressure.Health(7, 0.4), 9);
            // four of five: the group's size, above every floor's mercy
            Assert.Equal(0.8, TowerPressure.Health(1, 0.8), 9);
            for (var floor = 1; floor < 5; ++floor)
            {
                Assert.True(TowerPressure.Mercy(floor) <= TowerPressure.Mercy(floor + 1));
                Assert.True(TowerPressure.Mercy(floor) >= 0.5 && TowerPressure.Mercy(floor) < 1);
                // a small group's fight is allowed to be harder higher up
                Assert.True(TowerPressure.SmallGroupHardness(floor, 0.4) <= TowerPressure.SmallGroupHardness(floor + 1, 0.4));
            }
            // a guardian's blows shrink for a small group (for two of five more than its health), never grow, and
            // come back steadily as the group fills
            for (var floor = 1; floor <= 5; ++floor)
            {
                var last = 0.0;
                for (var pressure = 0.1; pressure < 1; pressure += 0.1)
                {
                    var blows = TowerPressure.BossBlows(floor, pressure);
                    Assert.InRange(blows, 0.01, 1.0);
                    Assert.True(blows >= last);
                    last = blows;
                }
                Assert.True(TowerPressure.BossBlows(floor, 0.4) < TowerPressure.Health(floor, 0.4));
            }
            // two of five on floor 1: half the health, and blows that make the fight a quarter harder than a full group's
            Assert.Equal(1.24, TowerPressure.SmallGroupHardness(1, 0.4), 9);
            Assert.Equal(1.24 * 0.16 / 0.5, TowerPressure.BossBlows(1, 0.4), 9);
            // and it all meets at a full group
            Assert.Equal(1.0, TowerPressure.BossBlows(3, 1.0), 9);
            Assert.Equal(1.0, TowerPressure.Health(3, 1.0), 9);
            Assert.Equal(1.0, TowerPressure.BossBlows(3, 0.9999999), 5);
        }

        [Fact]
        public void AGuardianUnderACrowdHasMoreHealthMoreHelpersAndBlowsALittleHarder()
        {
            var part = Tower.Get(25);
            var boss = Tower.Boss(part);
            var crowd = Tower.Boss(part, TowerPressure.Of(30, 1.0));
            Assert.Equal(TowerPressure.Scale(boss.Health, 5.0), crowd.Health);
            Assert.InRange(crowd.DamageScale, 1.01, 1 + TowerPressure.MaxBlows);
            Assert.True(TowerPressure.Helpers(part.Boss.AddsCount, part.Floor, 6) > part.Boss.AddsCount);
            // helpers: half of the scaling in their number, half in their health
            var helper = Tower.Enemy(part, part.Enemies[0], 6, true);
            var monster = Tower.Enemy(part, part.Enemies[0], 6);
            Assert.True(helper.Health < monster.Health);
            Assert.True(helper.Health > Tower.Enemy(part, part.Enemies[0]).Health);
            // its blows reach the sheet the fights use
            Assert.Equal(crowd.DamageScale, crowd.Sheet().DamageMultiplier, 9);
            Assert.Equal(1.0, boss.Sheet().DamageMultiplier, 9);
        }

        [Fact]
        public void AFreshSyncedPlayerHasStrengthOneAndWhatSyncDoesNotCapCountsForMore()
        {
            foreach (var step in new[] { 1, 7, 15, 30 })
            {
                var fresh = TowerPressure.Fresh(step);
                Assert.Equal(Tower.SyncLevel(step), fresh.CombatLevel);
                foreach (var style in new[] { AttackStyle.Melee, AttackStyle.Ranged, AttackStyle.Magic, AttackStyle.Healing })
                    Assert.Equal(1.0, TowerPressure.Strength(fresh, style, step), 6);

                // a level 999 character synced down with nothing sync does not cap is no stronger than a fresh one
                var veteran = TowerPressure.Fresh(step);
                Assert.True(TowerPressure.Strength(veteran, AttackStyle.Melee, step) <= 1.0001);

                // crits, damage and attack speed from trees and gems count
                var strong = TowerPressure.Fresh(step);
                strong.CritChance += 0.2;
                strong.DamageMultiplier = 1.2;
                strong.AttackSpeed = 1.1;
                var strength = TowerPressure.Strength(strong, AttackStyle.Melee, step);
                Assert.InRange(strength, 1.15, 1.6);
                // and every active skill in the loadout a little more
                Assert.Equal(strength + 3 * TowerPressure.StrengthPerActive, TowerPressure.Strength(strong, AttackStyle.Melee, step, 3), 9);

                // a player in worse gear is below one (the group's average is clamped, not the player)
                var weak = TowerPressure.Fresh(step);
                weak.Melee.Power /= 2;
                weak.Armor /= 2;
                Assert.True(TowerPressure.Strength(weak, AttackStyle.Melee, step) < 1);
            }
        }

        [Fact]
        public void TheMeanHitShareIsWhatTheDiceGive()
        {
            var random = new SeededRandom(5);
            var sum = 0.0;
            const int rolls = 200000;
            for (var i = 0; i < rolls; ++i) sum += CombatMath.RollHitShare(random);
            Assert.Equal(TowerPressure.MeanHitShare, sum / rolls, 2);
        }

        private sealed class SeededRandom : ICombatRandom
        {
            private readonly Random random;
            public SeededRandom(int seed) { random = new Random(seed); }
            public double Next() => random.NextDouble();
        }
    }
}
