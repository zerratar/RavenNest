using System;
using RavenNest.Combat;
using Xunit;

namespace RavenNest.Combat.Tests
{
    public class GemsTests
    {
        [Fact]
        public void ItemIdsAreFixedAndDistinct()
        {
            var seen = new System.Collections.Generic.HashSet<Guid>();
            foreach (GemKind k in Enum.GetValues(typeof(GemKind)))
            foreach (GemTier t in Enum.GetValues(typeof(GemTier)))
            {
                var id = Gems.ItemId(k, t);
                Assert.True(seen.Add(id));
                Assert.True(Gems.TryGetGem(id, out var kind, out var tier));
                Assert.Equal(k, kind);
                Assert.Equal(t, tier);
            }
            // the same name always gives the same id (the server's items depend on it)
            Assert.Equal(Gems.ItemId(GemKind.Ruby, GemTier.Perfect), Gems.ItemId(GemKind.Ruby, GemTier.Perfect));
            Assert.False(Gems.IsGem(Guid.NewGuid()));
        }

        [Fact]
        public void SocketsByLevel()
        {
            Assert.Equal(0, Gems.Sockets(24, GemSlot.Weapon));
            Assert.Equal(1, Gems.Sockets(25, GemSlot.Armor));
            Assert.Equal(2, Gems.Sockets(150, GemSlot.Weapon));
            Assert.Equal(3, Gems.Sockets(400, GemSlot.Armor));
            Assert.Equal(1, Gems.Sockets(999, GemSlot.Jewellery));
        }

        [Fact]
        public void BonusDependsOnSlotAndTier()
        {
            Assert.Equal(0.03, Gems.Bonus(GemKind.Ruby, GemTier.Chipped, GemSlot.Weapon)[GearStat.FireDamage], 6);
            Assert.Equal(0.30, Gems.Bonus(GemKind.Ruby, GemTier.Perfect, GemSlot.Weapon)[GearStat.FireDamage], 6);
            Assert.Equal(0.15, Gems.Bonus(GemKind.Ruby, GemTier.Perfect, GemSlot.Armor)[GearStat.FireResistance], 6);
            Assert.Equal(0.40, Gems.Bonus(GemKind.Ruby, GemTier.Perfect, GemSlot.Jewellery)[GearStat.BurnBonus], 6);
            Assert.Equal(0.05, Gems.Bonus(GemKind.Diamond, GemTier.Perfect, GemSlot.Weapon)[GearStat.CritChance], 6);
            Assert.Equal(0.15, Gems.Bonus(GemKind.Amethyst, GemTier.Perfect, GemSlot.Jewellery)[GearStat.CooldownReduction], 6);
        }

        [Fact]
        public void SocketTextRoundTrips()
        {
            var a = Gems.ItemId(GemKind.Topaz, GemTier.Flawed);
            var b = Gems.ItemId(GemKind.Onyx, GemTier.Flawless);
            var text = Gems.FormatSockets(new Guid?[] { a, null, b });
            var back = Gems.ParseSockets(text, 3);
            Assert.Equal(a, back[0]);
            Assert.Null(back[1]);
            Assert.Equal(b, back[2]);
            Assert.Null(Gems.FormatSockets(new Guid?[] { null, null }));
            Assert.Equal(a.ToString(), Gems.FormatSockets(new Guid?[] { a, null }));
            // two flawed topaz in a weapon: 12% lightning
            var bonus = Gems.SocketBonus(Gems.FormatSockets(new Guid?[] { a, a }), GemSlot.Weapon);
            Assert.Equal(0.12, bonus[GearStat.LightningDamage], 6);
        }

        [Fact]
        public void KeepChanceAndOdds()
        {
            Assert.Equal(0, Gems.KeepChance(99));
            Assert.Equal(0.40, Gems.KeepChance(400), 6);
            Assert.Equal(0.55, Gems.KeepChance(999, 3), 6);
            Assert.Equal(GemTier.Chipped, Gems.RollTier(1, 0.0));
            var odds = Gems.EnchantOdds(999);
            Assert.Equal(1.0, odds[0] + odds[1] + odds[2] + odds[3] + odds[4], 6);
            Assert.Equal(GemTier.Flawless, Gems.RollTier(999, 0.05));
            Assert.Equal(GemTier.Chipped, Gems.RollTier(999, 0.99));
        }

        [Fact]
        public void CritResistanceLowersCrits()
        {
            var g = new GearStats();
            g[GearStat.CritResistance] = 0.05;
            var sheet = new CombatSheet();
            g.ApplyExtras(sheet, null);
            Assert.Equal(0.05, sheet.CritResistance, 6);
            Assert.Equal("CRIT_RESISTANCE:5%", g.ToString());
        }
    }
}
