using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.Models;

namespace RavenNest.UnitTests
{
    /// <summary>"!equip tank", "!equip rogue": the words, and the rules the game and the server share for them.</summary>
    [TestClass]
    public class GearOutfitTests
    {
        [TestMethod]
        public void RoleWords_NameASkill()
        {
            Assert.AreEqual(Skill.Attack, GearOutfit.Parse("attack").Skill);
            Assert.AreEqual(Skill.Attack, GearOutfit.Parse("melee").Skill);
            Assert.AreEqual(Skill.Strength, GearOutfit.Parse("strength").Skill);
            Assert.AreEqual(Skill.Defense, GearOutfit.Parse("defense").Skill);
            Assert.AreEqual(Skill.Defense, GearOutfit.Parse("tank").Skill);
            foreach (var word in new[] { "ranged", "ranger", "archer", "bow" })
                Assert.AreEqual(Skill.Ranged, GearOutfit.Parse(word).Skill, word);
            Assert.AreEqual(Skill.Magic, GearOutfit.Parse("magic").Skill);
            Assert.AreEqual(Skill.Magic, GearOutfit.Parse("mage").Skill);
            Assert.AreEqual(Skill.Healing, GearOutfit.Parse("healing").Skill);
            Assert.AreEqual(Skill.Healing, GearOutfit.Parse("healer").Skill);

            // the short forms !train knows, and the other spelling of defense
            Assert.AreEqual(Skill.Attack, GearOutfit.Parse("atk").Skill);
            Assert.AreEqual(Skill.Strength, GearOutfit.Parse("str").Skill);
            Assert.AreEqual(Skill.Defense, GearOutfit.Parse("def").Skill);
            Assert.AreEqual(Skill.Defense, GearOutfit.Parse("defence").Skill);
            Assert.AreEqual(Skill.Ranged, GearOutfit.Parse("range").Skill);
            Assert.AreEqual(Skill.Healing, GearOutfit.Parse("heal").Skill);

            // a role is never one line only, and its lines are the skill's
            var tank = GearOutfit.Parse("tank");
            Assert.IsFalse(tank.OnlyLine);
            Assert.AreEqual(ItemLines.Fits(Skill.Defense), tank.Fits);
            Assert.AreEqual(ItemLine.Tank, tank.Line);
            Assert.AreEqual(ItemLine.Plate, GearOutfit.Parse("defense").Line);
        }

        [TestMethod]
        public void LineWords_NameOneLine()
        {
            var lines = new (string Word, ItemLine Line)[]
            {
                ("rogue", ItemLine.Rogue), ("barbarian", ItemLine.Barbarian), ("samurai", ItemLine.Samurai),
                ("paladin", ItemLine.Paladin), ("dark knight", ItemLine.DarkKnight), ("witch hunter", ItemLine.WitchHunter),
                ("monk", ItemLine.Monk), ("plate", ItemLine.Plate),
            };
            foreach (var (word, line) in lines)
            {
                var outfit = GearOutfit.Parse(word);
                Assert.IsNotNull(outfit, word);
                Assert.IsTrue(outfit.OnlyLine, word);
                Assert.AreEqual(line, outfit.Line, word);
                Assert.AreEqual(ItemLines.Bit(line), outfit.Fits, word);
            }

            // what an outfit covers: its worn pieces are only replaced by other pieces of it
            Assert.IsTrue(GearOutfit.Parse("paladin").Covers(ItemLines.Material(ItemLine.Paladin, 3)));
            Assert.IsFalse(GearOutfit.Parse("paladin").Covers(ItemLines.Material(ItemLine.Tank, 3)));
            Assert.IsFalse(GearOutfit.Parse("paladin").Covers(ItemMaterial.Rune));
            Assert.IsTrue(GearOutfit.Parse("plate").Covers(ItemMaterial.Rune));
            Assert.IsFalse(GearOutfit.Parse("plate").Covers(ItemLines.Material(ItemLine.Tank, 1)));
            // a role covers every line that fits its skill, and no plate
            Assert.IsTrue(GearOutfit.Parse("tank").Covers(ItemLines.Material(ItemLine.Paladin, 3)));
            Assert.IsTrue(GearOutfit.Parse("tank").Covers(ItemLines.Material(ItemLine.Tank, 8)));
            Assert.IsFalse(GearOutfit.Parse("tank").Covers(ItemLines.Material(ItemLine.Rogue, 8)));
            Assert.IsFalse(GearOutfit.Parse("tank").Covers(ItemMaterial.ElderAtlarus));
        }

        [TestMethod]
        public void ALineWhoseNameIsARole_IsAskedForWithLineAfterIt()
        {
            var lines = new (string Word, ItemLine Line)[]
            {
                ("tank line", ItemLine.Tank), ("melee line", ItemLine.Melee), ("ranger line", ItemLine.Ranger),
                ("mage line", ItemLine.Mage), ("healer line", ItemLine.Healer), ("Tank Line", ItemLine.Tank),
                ("rogue line", ItemLine.Rogue), ("dark knight line", ItemLine.DarkKnight),
            };
            foreach (var (word, line) in lines)
            {
                var outfit = GearOutfit.Parse(word);
                Assert.IsNotNull(outfit, word);
                Assert.IsTrue(outfit.OnlyLine, word);
                Assert.AreEqual(line, outfit.Line, word);
                Assert.AreEqual(line, GearOutfit.Parse(outfit.Word).Line, word);
            }
            Assert.IsNull(GearOutfit.Parse("line"));
            Assert.IsNull(GearOutfit.Parse("defense line"));
            Assert.IsNull(GearOutfit.Parse("fishing line"));
        }

        [TestMethod]
        public void AnOlderGame_SendsNoOutfit_AndGetsThePlainEquipBest()
        {
            // what the server's "equipment.equip-best" reads (CommandDispatcher): a game before 0.9.7 packs the
            // character alone, and the missing word is null, which is no outfit
            var character = System.Guid.NewGuid();
            RavenNest.Protocol.CommandArgs.Unpack(RavenNest.Protocol.CommandArgs.Pack(character), out System.Guid readCharacter, out string outfit);
            Assert.AreEqual(character, readCharacter);
            Assert.IsNull(outfit);
            Assert.IsNull(GearOutfit.Parse(outfit));

            RavenNest.Protocol.CommandArgs.Unpack(RavenNest.Protocol.CommandArgs.Pack(character, "tank"), out readCharacter, out outfit);
            Assert.AreEqual(character, readCharacter);
            Assert.AreEqual(Skill.Defense, GearOutfit.Parse(outfit).Skill);
        }

        [TestMethod]
        public void Words_AnyCase_WithOrWithoutTheSpace_AndNothingElse()
        {
            Assert.AreEqual("darkknight", GearOutfit.Parse("  Dark Knight ").Word);
            Assert.AreEqual("darkknight", GearOutfit.Parse("DARKKNIGHT").Word);
            Assert.AreEqual("witchhunter", GearOutfit.Parse("witch-hunter").Word);
            Assert.AreEqual("tank", GearOutfit.Parse("Tank").Word);

            // what the game sends is read back as the same outfit
            foreach (var word in new[] { "Tank", "dark knight", "Bow", "plate" })
            {
                var asked = GearOutfit.Parse(word);
                var read = GearOutfit.Parse(asked.Word);
                Assert.AreEqual(asked.Skill, read.Skill);
                Assert.AreEqual(asked.Line, read.Line);
                Assert.AreEqual(asked.OnlyLine, read.OnlyLine);
            }

            // only the whole argument: an item's name is not an outfit
            foreach (var text in new[] { null, "", "all", "tank helmet", "rune sword", "samurai helmet", "magic potion", "aegis cuirass" })
                Assert.IsNull(GearOutfit.Parse(text), text);
        }

        // a full tier 8 set of a line: five slots, each worth what the line's armor factor leaves of plate's 400
        private static void AddSet(System.Collections.Generic.List<ItemMaterial> materials, System.Collections.Generic.List<int> classics,
            System.Collections.Generic.List<int> slots, System.Collections.Generic.List<bool> worn, ItemLine line, int tier, bool isWorn, int pieces = 5)
        {
            for (var slot = 0; slot < pieces; ++slot)
            {
                materials.Add(ItemLines.Material(line, tier));
                classics.Add((int)System.Math.Ceiling(50 * tier * ItemLines.ArmorFactor(line)));
                slots.Add(slot);
                worn.Add(isWorn);
            }
        }

        [TestMethod]
        public void OneLine_TheNamedLineWinsAmongEquals_EvenWhenTheOtherIsWorn()
        {
            var materials = new System.Collections.Generic.List<ItemMaterial>();
            var classics = new System.Collections.Generic.List<int>();
            var slots = new System.Collections.Generic.List<int>();
            var worn = new System.Collections.Generic.List<bool>();
            AddSet(materials, classics, slots, worn, ItemLine.Tank, 8, false);
            AddSet(materials, classics, slots, worn, ItemLine.Paladin, 8, true);
            var fits = ItemLines.Fits(Skill.Defense);

            // plain "equip best" and "!equip defense": the set that is worn stays
            Assert.AreEqual(ItemLines.Bit(ItemLine.Paladin), GearScore.OneLine(fits, materials.ToArray(), classics.ToArray(), slots.ToArray(), worn.ToArray()));
            // "!equip tank": the Tank's
            Assert.AreEqual(ItemLines.Bit(ItemLine.Tank), GearScore.OneLine(fits, ItemLine.Tank, materials.ToArray(), classics.ToArray(), slots.ToArray(), worn.ToArray()));
            // (the worn Paladin set did not take it back); and asked for by name the Paladin's wins without being worn
            for (var i = 0; i < worn.Count; ++i) worn[i] = false;
            Assert.AreEqual(ItemLines.Bit(ItemLine.Paladin), GearScore.OneLine(fits, ItemLine.Paladin, materials.ToArray(), classics.ToArray(), slots.ToArray(), worn.ToArray()));
        }

        [TestMethod]
        public void OneLine_AClearlyBetterLineBeatsTheNamedOne()
        {
            var materials = new System.Collections.Generic.List<ItemMaterial>();
            var classics = new System.Collections.Generic.List<int>();
            var slots = new System.Collections.Generic.List<int>();
            var worn = new System.Collections.Generic.List<bool>();
            AddSet(materials, classics, slots, worn, ItemLine.Tank, 3, false);
            AddSet(materials, classics, slots, worn, ItemLine.Paladin, 8, false);
            var fits = ItemLines.Fits(Skill.Defense);
            Assert.AreEqual(ItemLines.Bit(ItemLine.Paladin), GearScore.OneLine(fits, ItemLine.Tank, materials.ToArray(), classics.ToArray(), slots.ToArray(), worn.ToArray()));

            // the named line with nothing owned: the best of the others
            materials.Clear(); classics.Clear(); slots.Clear(); worn.Clear();
            AddSet(materials, classics, slots, worn, ItemLine.DarkKnight, 5, false);
            Assert.AreEqual(ItemLines.Bit(ItemLine.DarkKnight), GearScore.OneLine(fits, ItemLine.Tank, materials.ToArray(), classics.ToArray(), slots.ToArray(), worn.ToArray()));
        }

        [TestMethod]
        public void OneLine_WithoutANamedLine_IsAsBefore()
        {
            var materials = new System.Collections.Generic.List<ItemMaterial>();
            var classics = new System.Collections.Generic.List<int>();
            var slots = new System.Collections.Generic.List<int>();
            var worn = new System.Collections.Generic.List<bool>();
            AddSet(materials, classics, slots, worn, ItemLine.Melee, 8, false);
            AddSet(materials, classics, slots, worn, ItemLine.Rogue, 8, false);
            AddSet(materials, classics, slots, worn, ItemLine.Samurai, 8, false, pieces: 3);
            var fits = ItemLines.Fits(Skill.Attack);
            var plain = GearScore.OneLine(fits, materials.ToArray(), classics.ToArray(), slots.ToArray(), worn.ToArray());
            Assert.AreEqual(plain, GearScore.OneLine(fits, ItemLine.Plate, materials.ToArray(), classics.ToArray(), slots.ToArray(), worn.ToArray()));
            Assert.AreEqual(ItemLines.Bit(ItemLine.Melee), plain); // equal sets: the first named, the skill's own line
            Assert.AreEqual(ItemLines.Bit(ItemLine.Rogue), GearScore.OneLine(fits, ItemLine.Rogue, materials.ToArray(), classics.ToArray(), slots.ToArray(), worn.ToArray()));
            // three pieces are not a match for five
            Assert.AreEqual(ItemLines.Bit(ItemLine.Melee), GearScore.OneLine(fits, ItemLine.Samurai, materials.ToArray(), classics.ToArray(), slots.ToArray(), worn.ToArray()));
        }

        [TestMethod]
        public void PickOfLine_MostWorth_ThenWorn_ThenFirstByName()
        {
            Assert.AreEqual(-1, GearScore.PickOfLine(new int[0], new bool[0], new string[0]));
            Assert.AreEqual(-1, GearScore.PickOfLine(new[] { 0 }, new[] { false }, new[] { "A look" }));
            Assert.AreEqual(1, GearScore.PickOfLine(new[] { 100, 433, 190 }, new[] { true, false, false }, new[] { "Guard Greathelm", "Aegis Greathelm", "Templar Greathelm" }));
            // a line's two head pieces are worth the same: the one worn stays
            Assert.AreEqual(1, GearScore.PickOfLine(new[] { 433, 433 }, new[] { false, true }, new[] { "Aegis Greathelm", "Aegis Visor Helm" }));
            // neither worn: the same one on both sides, whatever the order they come in
            Assert.AreEqual(0, GearScore.PickOfLine(new[] { 433, 433 }, new[] { false, false }, new[] { "Aegis Greathelm", "Aegis Visor Helm" }));
            Assert.AreEqual(1, GearScore.PickOfLine(new[] { 433, 433 }, new[] { false, false }, new[] { "Aegis Visor Helm", "Aegis Greathelm" }));
        }
    }
}
