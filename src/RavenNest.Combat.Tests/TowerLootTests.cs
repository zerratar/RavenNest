using System;
using RavenNest.Combat;
using Xunit;

namespace RavenNest.Combat.Tests
{
    public class TowerLootTests
    {
        // the class gear tiers' levels (RavenNest.Models.ItemLines.TierLevels)
        private static readonly int[] TierLevels = { 1, 30, 70, 120, 200, 400, 650, 900 };

        [Fact]
        public void EveryoneGetsAPieceOnAFirstClearAndOneInTenAfter()
        {
            Assert.Equal(1.0, TowerLoot.GearChance(true));
            Assert.Equal(0.10, TowerLoot.GearChance(false));
            Assert.True(TowerLoot.Drops(true, 0.999999));
            Assert.True(TowerLoot.Drops(false, 0.0999));
            Assert.False(TowerLoot.Drops(false, 0.10));
            Assert.False(TowerLoot.Drops(false, 0.95));
        }

        [Theory]
        [InlineData(1, 1)]    // level 10
        [InlineData(2, 1)]    // level 20
        [InlineData(3, 2)]    // level 30: the second tier's level exactly
        [InlineData(6, 2)]
        [InlineData(7, 3)]    // level 70
        [InlineData(11, 3)]
        [InlineData(12, 4)]   // level 120
        [InlineData(19, 4)]
        [InlineData(20, 5)]   // level 200
        [InlineData(30, 5)]
        [InlineData(39, 5)]
        [InlineData(40, 6)]   // level 400
        [InlineData(65, 7)]
        [InlineData(90, 8)]
        [InlineData(100, 8)]
        public void TheTierIsTheHighestAtOrBelowTheFloorsLevel(int floor, int tier)
        {
            Assert.Equal(tier, TowerLoot.GearTier(TierLevels, floor));
            // never a tier above the floor's level
            Assert.True(TierLevels[TowerLoot.GearTier(TierLevels, floor) - 1] <= Tower.Level(floor));
        }

        [Fact]
        public void TheTierOfAFloorThatDoesNotExistIsTheFirst()
        {
            Assert.Equal(1, TowerLoot.GearTier(TierLevels, 0));
            Assert.Equal(1, TowerLoot.GearTier(TierLevels, -3));
            Assert.Equal(1, TowerLoot.GearTier(null, 50));
        }

        [Fact]
        public void TheGemIsTheBandsElement()
        {
            Assert.Equal(GemKind.Ruby, TowerLoot.ElementGem(Element.Fire));
            Assert.Equal(GemKind.Sapphire, TowerLoot.ElementGem(Element.Ice));
            Assert.Equal(GemKind.Topaz, TowerLoot.ElementGem(Element.Lightning));
            Assert.Equal(GemKind.Emerald, TowerLoot.ElementGem(Element.Poison));
            Assert.Null(TowerLoot.ElementGem(Element.Physical));

            // every band that exists has an element, and a floor's gem is its band's whatever the line
            foreach (var band in Tower.Bands)
            {
                var gem = TowerLoot.ElementGem(band.Element);
                Assert.NotNull(gem);
                for (var floor = band.FirstFloor; floor <= band.LastFloor; ++floor)
                {
                    Assert.Equal(gem.Value, TowerLoot.Gem(floor, 0.4));
                    Assert.Equal(gem.Value, TowerLoot.Gem(floor, 1.0));
                }
            }
            Assert.Equal(GemKind.Emerald, TowerLoot.Gem(5, 1.0));   // the Overgrown Halls: poison
            Assert.Equal(GemKind.Sapphire, TowerLoot.Gem(15, 1.0)); // the Frozen Vault: ice
            Assert.Equal(GemKind.Topaz, TowerLoot.Gem(25, 1.0));    // the Storm Spire: lightning
        }

        [Fact]
        public void AFloorWithoutABandGetsTheLinesRoleGem()
        {
            var beyond = Tower.TopFloor + 500; // no band is defined up there
            Assert.Null(Tower.BandOf(beyond));
            Assert.Equal(GemKind.Onyx, TowerLoot.Gem(beyond, 1.0));      // Tank
            Assert.Equal(GemKind.Onyx, TowerLoot.Gem(beyond, 0.9));      // Paladin, Dark Knight
            Assert.Equal(GemKind.Diamond, TowerLoot.Gem(beyond, 0.75));  // Melee, Samurai
            Assert.Equal(GemKind.Diamond, TowerLoot.Gem(beyond, 0.6));   // Ranger, Rogue, Witch Hunter
            Assert.Equal(GemKind.Diamond, TowerLoot.Gem(beyond, 0.5));   // Barbarian
            Assert.Equal(GemKind.Amethyst, TowerLoot.Gem(beyond, 0.4));  // Mage, Healer, Monk
        }

        [Theory]
        [InlineData(1, GemTier.Chipped)]
        [InlineData(9, GemTier.Chipped)]
        [InlineData(10, GemTier.Flawed)]    // a band's last floor: one better
        [InlineData(11, GemTier.Flawed)]
        [InlineData(19, GemTier.Flawed)]
        [InlineData(20, GemTier.Polished)]
        [InlineData(25, GemTier.Flawed)]
        [InlineData(26, GemTier.Polished)]
        [InlineData(30, GemTier.Flawless)]
        [InlineData(49, GemTier.Polished)]
        [InlineData(50, GemTier.Flawless)]
        [InlineData(51, GemTier.Flawless)]
        [InlineData(75, GemTier.Flawless)]
        [InlineData(76, GemTier.Perfect)]
        [InlineData(100, GemTier.Perfect)]  // nothing above Perfect
        public void TheGemsTierGrowsWithTheFloor(int floor, GemTier tier)
        {
            Assert.Equal(tier, TowerLoot.GemTier(floor));
        }

        [Fact]
        public void TheGemsTierNeverGoesDownOnTheWayUpExceptAfterABandsLastFloor()
        {
            for (var floor = 2; floor <= 100; ++floor)
            {
                if ((floor - 1) % 10 == 0) continue; // the floor after a band's last
                Assert.True(TowerLoot.GemTier(floor) >= TowerLoot.GemTier(floor - 1), "floor " + floor);
            }
        }

        [Fact]
        public void TheGemSitsInTheFirstSocket()
        {
            var id = Gems.ItemId(GemKind.Sapphire, GemTier.Flawed);
            // one socket: the gem's id and nothing else
            Assert.Equal(id.ToString(), TowerLoot.Sockets(GemKind.Sapphire, GemTier.Flawed, 1));
            // three sockets: the first holds it, the others are empty (and left out of the text)
            var text = TowerLoot.Sockets(GemKind.Sapphire, GemTier.Flawed, 3);
            var sockets = Gems.ParseSockets(text, 3);
            Assert.Equal(id, sockets[0]);
            Assert.Null(sockets[1]);
            Assert.Null(sockets[2]);
            // the gem code reads its bonus from that text: ice resistance in armor
            Assert.Equal(Gems.Bonus(GemKind.Sapphire, GemTier.Flawed, GemSlot.Armor)[GearStat.IceResistance],
                Gems.SocketBonus(text, GemSlot.Armor)[GearStat.IceResistance], 9);
            Assert.True(Gems.SocketBonus(text, GemSlot.Armor)[GearStat.IceResistance] > 0);
            // a piece without sockets (tier 1, below item level 25) comes without a gem
            Assert.Null(TowerLoot.Sockets(GemKind.Sapphire, GemTier.Flawed, 0));
        }

        [Fact]
        public void PickCoversEveryChoiceAndStaysInside()
        {
            Assert.Equal(0, TowerLoot.Pick(6, 0));
            Assert.Equal(5, TowerLoot.Pick(6, 0.999999));
            Assert.Equal(5, TowerLoot.Pick(6, 1.0));   // a roll of exactly 1 must not fall off the end
            Assert.Equal(0, TowerLoot.Pick(6, -0.5));
            Assert.Equal(3, TowerLoot.Pick(6, 0.5));
            Assert.Equal(0, TowerLoot.Pick(1, 0.7));
            Assert.Equal(0, TowerLoot.Pick(0, 0.7));
            var seen = new bool[5];
            for (var i = 0; i < 100; ++i) seen[TowerLoot.Pick(5, i / 100.0)] = true;
            Assert.All(seen, s => Assert.True(s));
        }

        [Fact]
        public void ASocketedItemsNameSaysItsGems()
        {
            Assert.Equal("Sorcerer Robe", Gems.SocketedName("Sorcerer Robe", null));
            Assert.Equal("Sorcerer Robe", Gems.SocketedName("Sorcerer Robe", ""));
            Assert.Equal("Sorcerer Robe", Gems.SocketedName("Sorcerer Robe", "not-a-gem," + Guid.NewGuid()));
            var one = TowerLoot.Sockets(GemKind.Sapphire, GemTier.Polished, 2);
            Assert.Equal("Sorcerer Robe with a Polished Sapphire", Gems.SocketedName("Sorcerer Robe", one));
            var two = Gems.FormatSockets(new Guid?[] { Gems.ItemId(GemKind.Ruby, GemTier.Flawed), Gems.ItemId(GemKind.Topaz, GemTier.Chipped) });
            Assert.Equal("Aegis Cuirass with a Flawed Ruby and a Chipped Topaz", Gems.SocketedName("Aegis Cuirass", two));
            var three = Gems.FormatSockets(new Guid?[] { Gems.ItemId(GemKind.Ruby, GemTier.Flawed), Gems.ItemId(GemKind.Topaz, GemTier.Chipped), Gems.ItemId(GemKind.Onyx, GemTier.Perfect) });
            Assert.Equal("Aegis Cuirass with a Flawed Ruby, a Chipped Topaz and a Perfect Onyx", Gems.SocketedName("Aegis Cuirass", three));
            // an empty first socket and a gem in the second
            var second = Gems.FormatSockets(new Guid?[] { null, Gems.ItemId(GemKind.Emerald, GemTier.Flawless) });
            Assert.Equal("Rune Chest with a Flawless Emerald", Gems.SocketedName("Rune Chest", second));
        }
    }
}
