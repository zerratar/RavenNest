using System;
using RavenNest.Combat;
using Xunit;

namespace RavenNest.Combat.Tests
{
    public class SubstitutionTests
    {
        [Fact]
        public void AttributeChanceFollowsTheItemLevel()
        {
            Assert.Equal(0.35, Substitution.AttributeChance(1), 6);
            Assert.Equal(0.35, Substitution.AttributeChance(100), 6);
            Assert.Equal(0.43, Substitution.AttributeChance(370), 6); // 35 + 15 * 0.54 = 43.1, whole percents
            Assert.Equal(0.50, Substitution.AttributeChance(600), 6);
            Assert.Equal(0.50, Substitution.AttributeChance(1016), 6);
            for (var level = 1; level < 1000; level += 7)
            {
                var c = Substitution.AttributeChance(level);
                Assert.InRange(c, Substitution.AttributeMinChance, Substitution.AttributeMaxChance);
                Assert.True(c >= Substitution.AttributeChance(level - 7));
            }
        }

        [Fact]
        public void TheAttributeReadsFromTheEnchantment()
        {
            var p = Substitution.FromEnchantment("POWER:9%;SUBSTITUTION:42%");
            Assert.True(p.Has);
            Assert.Equal(0.42, p.Chance, 6);
            Assert.Equal(Substitution.AttributeCooldown, p.Cooldown);
            Assert.Equal(SubstitutionLook.SmokeStep, p.Look);
            Assert.Equal("SUBSTITUTION:42%", Substitution.Enchantment(0.42));
            Assert.False(Substitution.FromEnchantment("POWER:9%;CRIT_CHANCE:2%").Has);
            Assert.False(Substitution.FromEnchantment(null).Has);
            // a hand-written value over the attribute's top is held to it
            Assert.Equal(Substitution.AttributeMaxChance, Substitution.FromEnchantment("SUBSTITUTION:90%").Chance, 6);
            // it is a gear stat for names ("Substitution") and parsing
            Assert.True(GearStats.TryParseKey("SUBSTITUTION", out var stat));
            Assert.Equal(GearStat.Substitution, stat);
            Assert.Equal("Substitution", GearStats.Label(stat));
        }

        [Fact]
        public void EnchantingKeepsTheAttribute()
        {
            var rest = Substitution.WithoutAttribute("POWER:9%;SUBSTITUTION:42%;CRIT_CHANCE:2%", out var attribute);
            Assert.Equal("POWER:9%;CRIT_CHANCE:2%", rest);
            Assert.Equal("SUBSTITUTION:42%", attribute);
            Assert.Null(Substitution.WithoutAttribute("SUBSTITUTION:42%", out attribute));
            Assert.Equal("SUBSTITUTION:42%", attribute);
            Assert.Equal("ATTACK_SPEED:4%;SUBSTITUTION:42%", Substitution.WithAttribute("ATTACK_SPEED:4%", attribute));
            Assert.Equal("SUBSTITUTION:42%", Substitution.WithAttribute(null, attribute));
            Assert.Null(Substitution.WithAttribute(null, null));
            Assert.Equal("POWER:3%", Substitution.WithAttribute("POWER:3%", null));
        }

        [Fact]
        public void ShadowstoneTiers()
        {
            var chances = new[] { 0.40, 0.45, 0.50, 0.55, 0.60 };
            var cooldowns = new[] { 30.0, 27, 24, 21, 18 };
            for (var t = 0; t < Gems.TierCount; ++t)
            {
                var p = Substitution.FromGem((GemTier)t);
                Assert.Equal(chances[t], p.Chance, 6);
                Assert.Equal(cooldowns[t], p.Cooldown, 6);
                Assert.Equal(SubstitutionLook.ShadowPhase, p.Look);
            }
        }

        [Fact]
        public void TheShadowstoneIsFoundOnlyAndFitsArmorOnly()
        {
            Assert.False(Gems.IsCraftable(GemKind.Shadowstone));
            Assert.True(Gems.IsCraftable(GemKind.Amethyst));
            Assert.True(Gems.FitsIn(GemKind.Shadowstone, GemSlot.Armor));
            Assert.False(Gems.FitsIn(GemKind.Shadowstone, GemSlot.Weapon));
            Assert.False(Gems.FitsIn(GemKind.Shadowstone, GemSlot.Jewellery));
            // it has item ids of its own, and the seven made ones keep theirs (KindCount is still the seven)
            Assert.Equal(7, Gems.KindCount);
            var id = Gems.ItemId(GemKind.Shadowstone, GemTier.Flawless);
            Assert.True(Gems.IsShadowstone(id));
            Assert.False(Gems.IsShadowstone(Gems.ItemId(GemKind.Onyx, GemTier.Flawless)));
            Assert.Equal("Flawless Shadowstone", Gems.Name(GemKind.Shadowstone, GemTier.Flawless));
            Assert.True(Gems.TryParseKind("shadow", out var kind));
            Assert.Equal(GemKind.Shadowstone, kind);
            Assert.True(Gems.TryParseKind("s", out kind));
            Assert.Equal(GemKind.Sapphire, kind); // a short word still finds the old gem first
            // no gear stat: its power is Substitution's
            Assert.True(Gems.Bonus(GemKind.Shadowstone, GemTier.Perfect, GemSlot.Armor).IsEmpty);
            Assert.Equal("60% chance to escape a boss's area attack, every 18 s at most", Gems.Describe(GemKind.Shadowstone, GemTier.Perfect, GemSlot.Armor));
            Assert.Equal("only works in armor", Gems.Describe(GemKind.Shadowstone, GemTier.Perfect, GemSlot.Weapon));
        }

        [Fact]
        public void SocketsGiveThePowerInArmorOnly()
        {
            var sockets = Gems.FormatSockets(new Guid?[] { Gems.ItemId(GemKind.Ruby, GemTier.Flawed), Gems.ItemId(GemKind.Shadowstone, GemTier.Polished) });
            var armor = Substitution.FromSockets(sockets, GemSlot.Armor);
            Assert.Equal(0.50, armor.Chance, 6);
            Assert.Equal(24, armor.Cooldown, 6);
            Assert.False(Substitution.FromSockets(sockets, GemSlot.Weapon).Has);
            // two in one piece: the better one
            var two = Gems.FormatSockets(new Guid?[] { Gems.ItemId(GemKind.Shadowstone, GemTier.Chipped), Gems.ItemId(GemKind.Shadowstone, GemTier.Flawless) });
            Assert.Equal(0.55, Substitution.FromSockets(two, GemSlot.Armor).Chance, 6);
            // and its bonus adds nothing to the other gems' stats
            Assert.Equal(Gems.SocketBonus(Gems.FormatSockets(new Guid?[] { Gems.ItemId(GemKind.Ruby, GemTier.Flawed) }), GemSlot.Armor).ToString(),
                Gems.SocketBonus(sockets, GemSlot.Armor).ToString());
        }

        [Fact]
        public void SourcesNeverStack()
        {
            var attribute = Substitution.FromAttribute(0.48); // 48%, 30 s
            var chipped = Substitution.FromGem(GemTier.Chipped); // 40%, 30 s
            var perfect = Substitution.FromGem(GemTier.Perfect); // 60%, 18 s

            var a = Substitution.Best(attribute, chipped);
            Assert.Equal(0.48, a.Chance, 6);
            Assert.Equal(30, a.Cooldown, 6);
            Assert.Equal(SubstitutionLook.SmokeStep, a.Look);

            var b = Substitution.Best(attribute, perfect);
            Assert.Equal(0.60, b.Chance, 6);
            Assert.Equal(18, b.Cooldown, 6);
            Assert.Equal(SubstitutionLook.ShadowPhase, b.Look);

            // two attributes of 50%: still 50%, not 100%; a Flawed gem's shorter cooldown counts with the attribute's chance
            var c = Substitution.Best(Substitution.FromAttribute(0.5), Substitution.FromAttribute(0.5));
            Assert.Equal(0.5, c.Chance, 6);
            var d = Substitution.Best(Substitution.FromAttribute(0.5), Substitution.FromGem(GemTier.Flawed));
            Assert.Equal(0.5, d.Chance, 6);
            Assert.Equal(27, d.Cooldown, 6);
            Assert.Equal(SubstitutionLook.SmokeStep, d.Look);

            // a tie in chance and cooldown: the rarer look
            var e = Substitution.Best(Substitution.FromAttribute(0.5), Substitution.FromGem(GemTier.Polished));
            Assert.Equal(SubstitutionLook.ShadowPhase, e.Look);

            Assert.False(Substitution.Best(default, default).Has);
            Assert.Equal(0.48, Substitution.Best(default, attribute).Chance, 6);
            Assert.InRange(Substitution.Best(perfect, perfect).Chance, 0, Substitution.MaxChance);

            // one item with both
            var item = Substitution.FromItem("SUBSTITUTION:45%", Gems.FormatSockets(new Guid?[] { Gems.ItemId(GemKind.Shadowstone, GemTier.Flawless) }), GemSlot.Armor);
            Assert.Equal(0.55, item.Chance, 6);
            Assert.Equal(21, item.Cooldown, 6);
        }

        [Fact]
        public void TheCooldownHolds()
        {
            var p = Substitution.FromGem(GemTier.Chipped); // 40%, 30 s
            Assert.True(Substitution.Escapes(p, -1, 5, 0.1));
            Assert.False(Substitution.Escapes(p, -1, 5, 0.4)); // the roll must beat the chance
            Assert.False(Substitution.Escapes(p, 10, 39.9, 0.0)); // 29.9 s after the last: not ready
            Assert.True(Substitution.Escapes(p, 10, 40, 0.0));
            Assert.False(Substitution.Escapes(default, -1, 100, 0.0)); // no power, no escape
        }

        [Fact]
        public void EscapeRateOverManyBlows()
        {
            // blows every 6 s for ten minutes with a Chipped Shadowstone: about two blows in five escaped while it is ready,
            // never two within its 30 s
            var p = Substitution.FromGem(GemTier.Chipped);
            var rng = new Random(7);
            var last = -1.0;
            var escaped = 0;
            for (var t = 0.0; t < 600; t += 6)
            {
                if (Substitution.Escapes(p, last, t, rng.NextDouble()))
                {
                    Assert.True(last < 0 || t - last >= 30);
                    last = t;
                    escaped++;
                }
            }
            // at most one every 30 s (20 in ten minutes); with 40% at each ready blow about 11
            Assert.InRange(escaped, 7, 20);
        }

        [Fact]
        public void TowerSources()
        {
            Assert.Null(Substitution.TowerSource(1));
            Assert.Null(Substitution.TowerSource(9));
            Assert.Equal(SubstitutionSource.TowerFloorBoss, Substitution.TowerSource(10)); // floor 1's floor boss counts
            Assert.Equal(SubstitutionSource.TowerChest, Substitution.TowerSource(11)); // floor 2 from its first part
            Assert.Equal(SubstitutionSource.TowerFloorBoss, Substitution.TowerSource(20));
            Assert.Equal(SubstitutionSource.TowerChest, Substitution.TowerSource(25));
            Assert.False(Substitution.DungeonCounts(3));
            Assert.True(Substitution.DungeonCounts(4));
        }

        [Fact]
        public void DropOddsAreRare()
        {
            foreach (SubstitutionSource s in Enum.GetValues(typeof(SubstitutionSource)))
            {
                // the gem is rarer than the attribute from every source
                Assert.True(Substitution.GemChance(s) < Substitution.AttributeChance(s));
                Assert.True(Substitution.AttributeChance(s) <= 0.02);
            }

            // a regular stream's night: four raids of fifteen, a Legendary dungeon every other night with ten, a part and a
            // half of the climb with six players and a floor boss every ten parts. One find (either) every two to four weeks
            // of four nights, the gem alone every one to three months
            double Night(bool attribute, bool gem) => Substitution.NightFinds(4, 15, 0.5, 10, 1.35, 0.15, 6, attribute, gem);
            var nightsPerFind = 1 / Night(true, false);
            Assert.InRange(nightsPerFind, 8, 16);
            var nightsPerGem = 1 / Night(false, true);
            Assert.InRange(nightsPerGem, 20, 50);
            // and not every night on a big stream either (raids of forty, three parts with fifteen)
            Assert.True(Substitution.NightFinds(5, 40, 1, 25, 3, 0.3, 15) < 0.75);
        }

        [Fact]
        public void FoundGemTiers()
        {
            Assert.Equal(1.0, System.Linq.Enumerable.Sum(Substitution.GemTierOdds), 6);
            Assert.Equal(GemTier.Perfect, Substitution.RollGemTier(0.01));
            Assert.Equal(GemTier.Chipped, Substitution.RollGemTier(0.99));
            var counts = new int[Gems.TierCount];
            var rng = new Random(3);
            for (var i = 0; i < 100000; ++i) counts[(int)Substitution.RollGemTier(rng.NextDouble())]++;
            for (var t = 0; t < Gems.TierCount; ++t) Assert.InRange(counts[t] / 100000.0, Substitution.GemTierOdds[t] - 0.01, Substitution.GemTierOdds[t] + 0.01);
            // a floor boss: the better of two
            Assert.Equal(GemTier.Perfect, Substitution.RollGemTier(0.99, 0.01, better: true));
        }

        [Fact]
        public void Words()
        {
            Assert.Equal("45% chance to escape a boss's area attack, every 27 s at most", Substitution.Describe(Substitution.FromGem(GemTier.Flawed)));
            Assert.Equal("smoke step", Substitution.LookName(SubstitutionLook.SmokeStep));
            Assert.Equal("shadow phase", Substitution.LookName(SubstitutionLook.ShadowPhase));
        }
    }
}
