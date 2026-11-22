using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.Models;

namespace RavenNest.UnitTests
{
    /// <summary>
    /// Outfits per skill (docs/outfits-design.md in the game repo): the rules the game and the server share
    /// (RavenNest.Models.OutfitRules) and the text the outfits are kept as.
    /// </summary>
    [TestClass]
    public class OutfitRulesTests
    {
        private static readonly ItemType[] armorTypes = { ItemType.Helmet, ItemType.Chest, ItemType.Gloves, ItemType.Leggings, ItemType.Boots };

        // a piece of gear: its kind is made from its name, so the same name is the same kind of item
        private static OutfitPiece Piece(string name, ItemType type, ItemMaterial material, int classic, bool worn = false, bool wearable = true, bool plain = true, Guid? id = null)
        {
            return new OutfitPiece
            {
                Id = id ?? Guid.NewGuid(),
                ItemId = Kind(name),
                Type = type,
                Category = type == ItemType.Ring ? ItemCategory.Ring : type == ItemType.Amulet ? ItemCategory.Amulet
                    : OutfitRules.IsArmorSlot(OutfitRules.SlotOf(type)) || type == ItemType.Shield ? ItemCategory.Armor : ItemCategory.Weapon,
                Material = material,
                Classic = classic,
                Worn = worn,
                Wearable = wearable,
                Plain = plain,
                Name = name,
            };
        }

        private static Guid Kind(string name)
        {
            var bytes = new byte[16];
            var text = System.Text.Encoding.UTF8.GetBytes(name);
            for (var i = 0; i < text.Length; ++i) bytes[i % 16] = (byte)(bytes[i % 16] * 31 + text[i]);
            bytes[15] |= 1;
            return new Guid(bytes);
        }

        // five armor pieces of one material, each worth the same
        private static List<OutfitPiece> Set(string name, ItemMaterial material, int classic, bool worn)
        {
            return armorTypes.Select(type => Piece(name + " " + type, type, material, classic, worn)).ToList();
        }

        private static Outfit OutfitOf(Skill skill, IEnumerable<OutfitPiece> pieces, bool pinned = false)
        {
            var outfit = new Outfit { Skill = skill };
            foreach (var piece in pieces) outfit.Set(piece.Slot, piece.Id, piece.ItemId, pinned);
            return outfit;
        }

        // ---- the text ----------------------------------------------------------------------------------------------

        [TestMethod]
        public void Text_NothingKept_IsEmpty_AndEmptyReadsAsTheDefaults()
        {
            Assert.AreEqual(string.Empty, new CharacterOutfits().ToString());
            foreach (var text in new[] { null, "", "nonsense", "|||", "1;f;u;wx;c;b", "9;z9|x;y;z;1:2:3" })
            {
                var read = CharacterOutfits.Parse(text);
                Assert.IsNotNull(read, text);
                Assert.IsTrue(read.Follow, text);
                Assert.IsTrue(read.Upgrade, text);
                Assert.AreEqual(Skill.None, read.Worn, text);
                Assert.AreEqual(0, read.Outfits.Count, text);
            }
        }

        [TestMethod]
        public void Text_ReadsBackWhatWasWritten()
        {
            var outfits = new CharacterOutfits { Follow = false, Upgrade = false, Worn = Skill.Farming, Fight = Skill.Attack, Back = Skill.Farming };
            var attack = outfits.GetOrAdd(Skill.Attack);
            attack.Word = "Dark Knight";
            attack.Pet = Guid.NewGuid();
            var helmet = attack.Set(EquipmentSlot.Head, Guid.NewGuid(), Guid.NewGuid(), true);
            var sword = attack.Set(EquipmentSlot.MeleeWeapon, Guid.Empty, Guid.NewGuid(), false);
            var farming = outfits.GetOrAdd(Skill.Farming);
            farming.Set(EquipmentSlot.Ring, Guid.NewGuid(), Guid.NewGuid(), false);
            outfits.GetOrAdd(Skill.Health);

            var text = outfits.ToString();
            Assert.IsFalse(text.Contains(" "), "one short text, no spaces");
            var read = CharacterOutfits.Parse(text);
            Assert.AreEqual(text, read.ToString());
            Assert.IsFalse(read.Follow);
            Assert.IsFalse(read.Upgrade);
            Assert.AreEqual(Skill.Farming, read.Worn);
            Assert.AreEqual(Skill.Attack, read.Fight);
            Assert.AreEqual(Skill.Farming, read.Back);
            Assert.AreEqual(3, read.Outfits.Count);

            var readAttack = read.Find(Skill.Attack);
            Assert.AreEqual("darkknight", readAttack.Word, "kept as the word both sides know");
            Assert.AreEqual(attack.Pet, readAttack.Pet);
            Assert.AreEqual(2, readAttack.Slots.Count);
            Assert.AreEqual(helmet.Item, readAttack.Get(EquipmentSlot.Head).Item);
            Assert.AreEqual(helmet.ItemId, readAttack.Get(EquipmentSlot.Head).ItemId);
            Assert.IsTrue(readAttack.Get(EquipmentSlot.Head).Pinned);
            Assert.AreEqual(Guid.Empty, readAttack.Get(EquipmentSlot.MeleeWeapon).Item, "a piece known by its kind only");
            Assert.AreEqual(sword.ItemId, readAttack.Get(EquipmentSlot.MeleeWeapon).ItemId);
            Assert.IsFalse(readAttack.Get(EquipmentSlot.MeleeWeapon).Pinned);
            Assert.AreEqual(1, read.Find(Skill.Farming).Slots.Count);
            Assert.AreEqual(0, read.Find(Skill.Health).Slots.Count);
            Assert.IsNull(read.Find(Skill.Mining));
        }

        [TestMethod]
        public void Text_SkipsWhatItDoesNotKnow()
        {
            var kind = Guid.NewGuid().ToString("N");
            // a field, a slot's fourth value and an outfit's fifth field from a newer version; a pet slot, which no outfit has
            var read = CharacterOutfits.Parse("2;f0;x7;w6;q|6;;;3::" + kind + ":p:later,13::" + kind + ";later|900;;;|17;;;");
            Assert.IsFalse(read.Follow);
            Assert.IsTrue(read.Upgrade);
            Assert.AreEqual(Skill.Mining, read.Worn);
            Assert.AreEqual(2, read.Outfits.Count, "Taming has no outfit");
            Assert.IsNotNull(read.Find(Skill.Health), "Melee is training all, which is kept as Health");
            Assert.AreEqual(1, read.Find(Skill.Mining).Slots.Count);
            Assert.IsTrue(read.Find(Skill.Mining).IsPinned(EquipmentSlot.Ring));
        }

        // ---- skills ------------------------------------------------------------------------------------------------

        [TestMethod]
        public void Skills_AsOutfitsKnowThem()
        {
            Assert.AreEqual(Skill.Health, OutfitRules.Normalize(Skill.Melee));
            Assert.AreEqual(Skill.None, OutfitRules.Normalize(Skill.Taming));
            Assert.AreEqual(Skill.None, OutfitRules.Normalize((Skill)123));
            Assert.AreEqual(Skill.Alchemy, OutfitRules.Normalize(Skill.Alchemy));

            foreach (var skill in new[] { Skill.Attack, Skill.Defense, Skill.Strength, Skill.Health, Skill.Melee, Skill.Ranged, Skill.Magic, Skill.Healing })
                Assert.IsTrue(OutfitRules.IsCombat(skill), skill.ToString());
            foreach (var skill in new[] { Skill.Mining, Skill.Farming, Skill.Cooking, Skill.Sailing, Skill.Slayer, Skill.Taming, Skill.None })
                Assert.IsFalse(OutfitRules.IsCombat(skill), skill.ToString());

            // the task the server keeps
            Assert.AreEqual(Skill.Attack, OutfitRules.SkillOf("Fighting", "attack"));
            Assert.AreEqual(Skill.Healing, OutfitRules.SkillOf("Fighting", "Healing"));
            Assert.AreEqual(Skill.Health, OutfitRules.SkillOf("Fighting", "all"));
            Assert.AreEqual(Skill.Health, OutfitRules.SkillOf("fighting", "Health"));
            Assert.AreEqual(Skill.Health, OutfitRules.SkillOf("Fighting", null));
            Assert.AreEqual(Skill.Mining, OutfitRules.SkillOf("Mining", "Mining"));
            Assert.AreEqual(Skill.Alchemy, OutfitRules.SkillOf("Alchemy", null));
            Assert.AreEqual(Skill.None, OutfitRules.SkillOf(null, "attack"));
            Assert.AreEqual(Skill.None, OutfitRules.SkillOf("Dancing", null));

            // what a player types
            Assert.AreEqual(Skill.Attack, OutfitRules.ParseSkill("Attack"));
            Assert.AreEqual(Skill.Attack, OutfitRules.ParseSkill("atk"));
            Assert.AreEqual(Skill.Defense, OutfitRules.ParseSkill("def"));
            Assert.AreEqual(Skill.Defense, OutfitRules.ParseSkill("tank"));
            Assert.AreEqual(Skill.Ranged, OutfitRules.ParseSkill("ranger"));
            Assert.AreEqual(Skill.Healing, OutfitRules.ParseSkill("heal"));
            Assert.AreEqual(Skill.Health, OutfitRules.ParseSkill("all"));
            Assert.AreEqual(Skill.Woodcutting, OutfitRules.ParseSkill("wood"));
            Assert.AreEqual(Skill.Mining, OutfitRules.ParseSkill(" MINING "));
            Assert.AreEqual(Skill.Alchemy, OutfitRules.ParseSkill("brewing"));
            foreach (var text in new[] { null, "", "save", "auto", "rogue", "plate", "taming", "none", "rune sword" })
                Assert.AreEqual(Skill.None, OutfitRules.ParseSkill(text), text);

            Assert.AreEqual("all", OutfitRules.NameOf(Skill.Health));
            Assert.AreEqual("woodcutting", OutfitRules.NameOf(Skill.Woodcutting));
            foreach (var skill in new[] { Skill.Attack, Skill.Health, Skill.Mining, Skill.Alchemy, Skill.Sailing })
                Assert.AreEqual(skill, OutfitRules.ParseSkill(OutfitRules.NameOf(skill)), skill.ToString());
        }

        [TestMethod]
        public void AFight_IsFoughtInTheOutfitOfTheCombatSkillTrainedOrTrainedLast()
        {
            // not in a fight: what is trained
            Assert.AreEqual(Skill.Mining, OutfitRules.Desired(Skill.Mining, false, Skill.Attack));
            Assert.AreEqual(Skill.Ranged, OutfitRules.Desired(Skill.Ranged, false, Skill.Attack));
            // in a fight: the combat skill that is trained
            Assert.AreEqual(Skill.Ranged, OutfitRules.Desired(Skill.Ranged, true, Skill.Attack));
            Assert.AreEqual(Skill.Health, OutfitRules.Desired(Skill.Melee, true, Skill.Attack));
            // a gathering character: the combat skill it trained last, or its own outfit when it never trained one
            Assert.AreEqual(Skill.Attack, OutfitRules.Desired(Skill.Mining, true, Skill.Attack));
            Assert.AreEqual(Skill.Mining, OutfitRules.Desired(Skill.Mining, true, Skill.None));
            Assert.AreEqual(Skill.Mining, OutfitRules.Desired(Skill.Mining, true, Skill.Fishing));
            Assert.AreEqual(Skill.None, OutfitRules.Desired(Skill.None, true, Skill.Attack));
        }

        [TestMethod]
        public void TheGameAsksTheServer_OnlyWhenSomethingWouldChange()
        {
            var outfits = new CharacterOutfits { Worn = Skill.Mining, Fight = Skill.Attack };
            // the same training, no fight: nothing to ask
            Assert.IsFalse(OutfitRules.NeedsAsking(outfits, Skill.Mining, false, Skill.Mining));
            // training changed
            Assert.IsTrue(OutfitRules.NeedsAsking(outfits, Skill.Fishing, false, Skill.Mining));
            // a fight begins and the combat skill has no outfit of its own: what is worn stays, nothing to ask
            Assert.IsFalse(OutfitRules.NeedsAsking(outfits, Skill.Mining, true, Skill.Mining));
            // with an outfit for it, the server is asked, and again when the fight is over
            outfits.GetOrAdd(Skill.Attack);
            Assert.IsTrue(OutfitRules.NeedsAsking(outfits, Skill.Mining, true, Skill.Mining));
            Assert.IsTrue(OutfitRules.NeedsAsking(outfits, Skill.Mining, false, Skill.Attack));
            Assert.IsFalse(OutfitRules.NeedsAsking(outfits, Skill.Mining, true, Skill.Attack));
            // someone who trains combat joins a raid: their outfit is on
            Assert.IsFalse(OutfitRules.NeedsAsking(outfits, Skill.Attack, true, Skill.Attack));
            // a new combat skill is told to the server, also with the switch off
            Assert.IsTrue(OutfitRules.NeedsAsking(outfits, Skill.Ranged, false, Skill.Ranged));
            outfits.Follow = false;
            Assert.IsTrue(OutfitRules.NeedsAsking(outfits, Skill.Ranged, false, Skill.Mining));
            Assert.IsFalse(OutfitRules.NeedsAsking(outfits, Skill.Attack, false, Skill.Mining));
            Assert.IsFalse(OutfitRules.NeedsAsking(outfits, Skill.Fishing, false, Skill.Mining));
            Assert.IsFalse(OutfitRules.NeedsAsking(outfits, Skill.Mining, true, Skill.Mining));
            // no task, no question
            Assert.IsFalse(OutfitRules.NeedsAsking(new CharacterOutfits(), Skill.None, false, Skill.None));
            Assert.IsFalse(OutfitRules.NeedsAsking(null, Skill.Attack, false, Skill.None));
        }

        // ---- remembering -------------------------------------------------------------------------------------------

        [TestMethod]
        public void LeavingASkill_KeepsWhatIsWornAsItsOutfit()
        {
            var pieces = Set("Rune", ItemMaterial.Rune, 100, worn: true);
            pieces.Add(Piece("Rune Sword", ItemType.OneHandedSword, ItemMaterial.Rune, 120, worn: true));
            pieces.Add(Piece("Dragon Sword", ItemType.OneHandedSword, ItemMaterial.Dragon, 200));
            pieces.Add(Piece("Gold Ring", ItemType.Ring, ItemMaterial.None, 10));
            var pet = Guid.NewGuid();

            var outfit = new Outfit { Skill = Skill.Attack };
            Assert.IsTrue(OutfitRules.Remember(outfit, pieces, pet));
            Assert.AreEqual(6, outfit.Slots.Count, "the five armor pieces and the sword; the ring in the bag is not worn");
            Assert.AreEqual(Kind("Rune Sword"), outfit.Get(EquipmentSlot.MeleeWeapon).ItemId);
            Assert.IsNull(outfit.Get(EquipmentSlot.Ring));
            Assert.AreEqual(pet, outfit.Pet);
            Assert.AreEqual(0, outfit.PinnedCount, "remembered slots are \"best\"");

            // nothing changed: nothing to save
            Assert.IsFalse(OutfitRules.Remember(outfit, pieces, pet));
        }

        [TestMethod]
        public void Remembering_LeavesPinnedSlotsAlone_AndForgetsEmptyOnes()
        {
            var pinnedRing = Piece("Phantom Ring", ItemType.Ring, ItemMaterial.Phantom, 50);
            var wornRing = Piece("Gold Ring", ItemType.Ring, ItemMaterial.None, 10, worn: true);
            var sword = Piece("Rune Sword", ItemType.OneHandedSword, ItemMaterial.Rune, 120);
            var pieces = new List<OutfitPiece> { pinnedRing, wornRing, sword };

            var outfit = new Outfit { Skill = Skill.Mining, Pet = Guid.NewGuid() };
            outfit.Set(EquipmentSlot.Ring, pinnedRing.Id, pinnedRing.ItemId, true);
            outfit.Set(EquipmentSlot.MeleeWeapon, sword.Id, sword.ItemId, false);

            Assert.IsTrue(OutfitRules.Remember(outfit, pieces, Guid.Empty));
            Assert.AreEqual(pinnedRing.ItemId, outfit.Get(EquipmentSlot.Ring).ItemId, "the pin holds, whatever ring is worn");
            Assert.IsTrue(outfit.IsPinned(EquipmentSlot.Ring));
            Assert.IsNull(outfit.Get(EquipmentSlot.MeleeWeapon), "no sword is worn: none is kept");
            Assert.AreEqual(Guid.Empty, outfit.Pet, "no pet is out: the outfit has no say about the pet");
        }

        [TestMethod]
        public void Pinning_PinsWhatIsWorn_AndUnpinningLetsItUpgradeAgain()
        {
            var pieces = Set("Rune", ItemMaterial.Rune, 100, worn: true);
            var outfit = new Outfit { Skill = Skill.Defense };
            outfit.Set(EquipmentSlot.Ring, Guid.NewGuid(), Guid.NewGuid(), true);
            OutfitRules.Pin(outfit, pieces, Guid.Empty);
            Assert.AreEqual(5, outfit.PinnedCount);
            Assert.IsNull(outfit.Get(EquipmentSlot.Ring), "no ring is worn");
            Assert.AreEqual(5, OutfitRules.Unpin(outfit));
            Assert.AreEqual(0, outfit.PinnedCount);
            Assert.AreEqual(5, outfit.Slots.Count);
            Assert.AreEqual(0, OutfitRules.Unpin(null));
        }

        // ---- wearing -----------------------------------------------------------------------------------------------

        [TestMethod]
        public void WearingAnOutfit_PutsItsPiecesOn_AndLeavesTheOtherSlots()
        {
            var plate = Set("Rune", ItemMaterial.Rune, 100, worn: true);
            var ranger = Set("Windrunner", ItemLines.Material(ItemLine.Ranger, 7), 60, worn: false);
            var ring = Piece("Gold Ring", ItemType.Ring, ItemMaterial.None, 10, worn: true);
            var pieces = plate.Concat(ranger).Append(ring).ToList();

            var outfit = OutfitOf(Skill.Ranged, ranger);
            var before = outfit.Slots.Count;
            var plan = OutfitRules.Wear(outfit, pieces);
            CollectionAssert.AreEquivalent(ranger.Select(x => x.Id).ToList(), plan.Equip.Select(i => pieces[i].Id).ToList());
            Assert.AreEqual(0, plan.Gone.Count);
            Assert.IsFalse(plan.Changed);
            Assert.AreEqual(before, outfit.Slots.Count, "the ring's slot is not the outfit's: the ring stays on");

            // on already: nothing to put on
            var worn = pieces.Select(x => { x.Worn = ranger.Any(r => r.Id == x.Id) || x.Id == ring.Id; return x; }).ToList();
            Assert.AreEqual(0, OutfitRules.Wear(outfit, worn).Equip.Count);
        }

        [TestMethod]
        public void APlainPiece_IsFoundByItsKind_WhenItsStackHasANewId()
        {
            // the outfit kept the helmet under the id it was worn with; taken off, it joined its stack under another id
            var helmet = Piece("Rune Helmet", ItemType.Helmet, ItemMaterial.Rune, 100);
            var outfit = new Outfit { Skill = Skill.Attack };
            outfit.Set(EquipmentSlot.Head, Guid.NewGuid(), helmet.ItemId, true);
            var pieces = new List<OutfitPiece> { helmet, Piece("Bronze Helmet", ItemType.Helmet, ItemMaterial.Bronze, 10, worn: true) };

            var plan = OutfitRules.Wear(outfit, pieces);
            Assert.AreEqual(0, plan.Gone.Count, "the same kind of item is the piece");
            Assert.AreEqual(1, plan.Equip.Count);
            Assert.AreEqual(helmet.Id, pieces[plan.Equip[0]].Id);
            Assert.IsTrue(outfit.IsPinned(EquipmentSlot.Head), "found, so the pin stays");
            Assert.AreEqual(helmet.Id, outfit.Get(EquipmentSlot.Head).Item, "and the outfit points at the stack it is in now");
            Assert.IsTrue(plan.Changed);
        }

        [TestMethod]
        public void APlainPieceThatIsWorn_IsNotSwappedForItsOwnStack()
        {
            // one of three plain helmets is worn (under its own id); the outfit kept the stack's id
            var stack = Piece("Rune Helmet", ItemType.Helmet, ItemMaterial.Rune, 100);
            var worn = Piece("Rune Helmet", ItemType.Helmet, ItemMaterial.Rune, 100, worn: true);
            var outfit = new Outfit { Skill = Skill.Attack };
            outfit.Set(EquipmentSlot.Head, stack.Id, stack.ItemId, true);
            var plan = OutfitRules.Wear(outfit, new List<OutfitPiece> { stack, worn });
            Assert.AreEqual(0, plan.Equip.Count);
            Assert.AreEqual(worn.Id, outfit.Get(EquipmentSlot.Head).Item);

            // an enchanted piece is itself and no other: it goes on though a plain one of its kind is worn
            var enchanted = Piece("Rune Helmet", ItemType.Helmet, ItemMaterial.Rune, 130, plain: false);
            outfit.Set(EquipmentSlot.Head, enchanted.Id, enchanted.ItemId, true);
            var pieces = new List<OutfitPiece> { stack, worn, enchanted };
            plan = OutfitRules.Wear(outfit, pieces);
            Assert.AreEqual(1, plan.Equip.Count);
            Assert.AreEqual(enchanted.Id, pieces[plan.Equip[0]].Id);
        }

        [TestMethod]
        public void APieceThatIsGone_IsFilledWithTheBestOwned_AndToldOnce()
        {
            var soldHelmet = Kind("Elder Atlarus Helmet");
            var chest = Piece("Rune Chest", ItemType.Chest, ItemMaterial.Rune, 100);
            var outfit = new Outfit { Skill = Skill.Attack };
            outfit.Set(EquipmentSlot.Head, Guid.NewGuid(), soldHelmet, true);
            outfit.Set(EquipmentSlot.Chest, chest.Id, chest.ItemId, false);
            var pieces = new List<OutfitPiece>
            {
                chest,
                Piece("Bronze Helmet", ItemType.Helmet, ItemMaterial.Bronze, 10, worn: true),
                Piece("Dragon Helmet", ItemType.Helmet, ItemMaterial.Dragon, 150),
                Piece("Phantom Helmet", ItemType.Helmet, ItemMaterial.Phantom, 300, wearable: false),
                Piece("Santa Hat", ItemType.Hat, ItemMaterial.None, 500),
            };

            var plan = OutfitRules.Wear(outfit, pieces);
            CollectionAssert.AreEqual(new[] { soldHelmet }, plan.Gone.ToArray());
            CollectionAssert.AreEqual(new[] { Kind("Dragon Helmet") }, plan.Filled.ToArray(), "the best that may be worn; a hat is a look");
            Assert.IsTrue(plan.Changed);
            Assert.AreEqual(Kind("Dragon Helmet"), outfit.Get(EquipmentSlot.Head).ItemId);
            Assert.IsFalse(outfit.IsPinned(EquipmentSlot.Head), "the pin was on the piece that is gone");
            Assert.AreEqual(2, plan.Equip.Count);

            // the next time the outfit has the new piece: nothing is gone, nothing is told again
            plan = OutfitRules.Wear(outfit, pieces);
            Assert.AreEqual(0, plan.Gone.Count);
            Assert.IsFalse(plan.Changed);
        }

        [TestMethod]
        public void APieceThatIsGone_WithNothingToFillIt_LeavesTheSlotAsItIs()
        {
            var outfit = new Outfit { Skill = Skill.Mining };
            outfit.Set(EquipmentSlot.Amulet, Guid.NewGuid(), Kind("Gold Amulet"), false);
            var pieces = new List<OutfitPiece> { Piece("Rune Helmet", ItemType.Helmet, ItemMaterial.Rune, 100, worn: true) };
            var plan = OutfitRules.Wear(outfit, pieces);
            Assert.AreEqual(1, plan.Gone.Count);
            Assert.AreEqual(Guid.Empty, plan.Filled[0]);
            Assert.AreEqual(0, plan.Equip.Count);
            Assert.IsNull(outfit.Get(EquipmentSlot.Amulet), "nothing is kept for the slot any more");
        }

        [TestMethod]
        public void AGonePiece_IsFilledFromTheLineTheOutfitIsOf()
        {
            // a mining outfit made of ranger gear (a skill that names no line): its chest was sold. Plate is worth
            // more by the old rule, but the set stays whole when there is another ranger chest
            var ranger = Set("Windrunner", ItemLines.Material(ItemLine.Ranger, 7), 60, worn: false);
            var outfit = OutfitOf(Skill.Mining, ranger);
            var pieces = ranger.Where(x => x.Type != ItemType.Chest).ToList();
            pieces.Add(Piece("Warden Chest", ItemType.Chest, ItemLines.Material(ItemLine.Ranger, 6), 54));
            pieces.Add(Piece("Rune Chest", ItemType.Chest, ItemMaterial.Rune, 62));
            var plan = OutfitRules.Wear(outfit, pieces);
            Assert.AreEqual(Kind("Warden Chest"), plan.Filled[0]);

            // an attack outfit with "!equip tank" asked for it: a tank piece before a rogue's that fits attack better
            var tank = Set("Aegis", ItemLines.Material(ItemLine.Tank, 8), 100, worn: false);
            var asked = OutfitOf(Skill.Attack, tank);
            asked.Word = "tank";
            pieces = tank.Where(x => x.Type != ItemType.Boots).ToList();
            pieces.Add(Piece("Bulwark Boots", ItemType.Boots, ItemLines.Material(ItemLine.Tank, 7), 90));
            pieces.Add(Piece("Nightfall Boots", ItemType.Boots, ItemLines.Material(ItemLine.Rogue, 8), 60));
            pieces.Add(Piece("Elder Atlarus Boots", ItemType.Boots, ItemMaterial.ElderAtlarus, 400));
            plan = OutfitRules.Wear(asked, pieces);
            Assert.AreEqual(Kind("Bulwark Boots"), plan.Filled[0]);
        }

        [TestMethod]
        public void AnOutfitThatIsOn_OnlyPutsItsPinnedPiecesBack()
        {
            var pinned = Piece("Phantom Ring", ItemType.Ring, ItemMaterial.Phantom, 50);
            var sword = Piece("Rune Sword", ItemType.OneHandedSword, ItemMaterial.Rune, 120);
            var outfit = new Outfit { Skill = Skill.Attack };
            outfit.Set(EquipmentSlot.Ring, pinned.Id, pinned.ItemId, true);
            outfit.Set(EquipmentSlot.MeleeWeapon, sword.Id, sword.ItemId, false);
            var pieces = new List<OutfitPiece> { pinned, sword, Piece("Gold Ring", ItemType.Ring, ItemMaterial.None, 10, worn: true), Piece("Dragon Sword", ItemType.OneHandedSword, ItemMaterial.Dragon, 200, worn: true) };
            var plan = OutfitRules.Wear(outfit, pieces, pinnedOnly: true);
            Assert.AreEqual(1, plan.Equip.Count);
            Assert.AreEqual(pinned.Id, pieces[plan.Equip[0]].Id);
        }

        // ---- gear that arrives -------------------------------------------------------------------------------------

        [TestMethod]
        public void ABetterPieceThatArrives_GoesIntoAnOutfitsBestSlot_AndNeverIntoAPinnedOne()
        {
            var sword = Piece("Rune Sword", ItemType.OneHandedSword, ItemMaterial.Rune, 120);
            var helmet = Piece("Rune Helmet", ItemType.Helmet, ItemMaterial.Rune, 100);
            var pieces = new List<OutfitPiece> { sword, helmet };
            var outfit = new Outfit { Skill = Skill.Attack };
            outfit.Set(EquipmentSlot.MeleeWeapon, sword.Id, sword.ItemId, false);
            outfit.Set(EquipmentSlot.Head, helmet.Id, helmet.ItemId, true);

            var betterSword = Piece("Dragon Sword", ItemType.OneHandedSword, ItemMaterial.Dragon, 200, id: Guid.Empty);
            var worseSword = Piece("Bronze Sword", ItemType.OneHandedSword, ItemMaterial.Bronze, 20);
            var betterHelmet = Piece("Dragon Helmet", ItemType.Helmet, ItemMaterial.Dragon, 150);
            Assert.IsTrue(OutfitRules.Upgrades(outfit, betterSword, pieces), "not in the bag yet, judged all the same");
            Assert.IsFalse(OutfitRules.Upgrades(outfit, worseSword, pieces));
            Assert.IsFalse(OutfitRules.Upgrades(outfit, betterHelmet, pieces), "pinned");
            outfit.Get(EquipmentSlot.Head).Pinned = false;
            Assert.IsTrue(OutfitRules.Upgrades(outfit, betterHelmet, pieces));

            // not for a character that cannot wear it, not a look, not a slot the outfit keeps nothing for
            var tooHigh = betterSword; tooHigh.Wearable = false;
            Assert.IsFalse(OutfitRules.Upgrades(outfit, tooHigh, pieces));
            Assert.IsFalse(OutfitRules.Upgrades(outfit, Piece("Santa Hat", ItemType.Hat, ItemMaterial.None, 500), pieces));
            Assert.IsFalse(OutfitRules.Upgrades(outfit, Piece("Gold Ring", ItemType.Ring, ItemMaterial.None, 10), pieces));
            // a piece that is gone is filled when the outfit is next worn, not here
            Assert.IsFalse(OutfitRules.Upgrades(outfit, betterSword, new List<OutfitPiece> { helmet }));
            // the same kind again is no upgrade
            Assert.IsFalse(OutfitRules.Upgrades(outfit, Piece("Rune Sword", ItemType.OneHandedSword, ItemMaterial.Rune, 120), pieces));
        }

        [TestMethod]
        public void AnArrivingPiece_IsRankedForTheOutfitsOwnSkill()
        {
            // a ranged outfit in ranger gear, a mining outfit in the same plate as it happens: a better ranger chest
            // goes into the ranged outfit, and plate that is worth more by the old rule does not
            var ranger = Set("Warden", ItemLines.Material(ItemLine.Ranger, 6), 54, worn: false);
            var plate = Set("Rune", ItemMaterial.Rune, 62, worn: true);
            var pieces = ranger.Concat(plate).ToList();
            var ranged = OutfitOf(Skill.Ranged, ranger);
            var mining = OutfitOf(Skill.Mining, plate);

            var windrunner = Piece("Windrunner Chest", ItemType.Chest, ItemLines.Material(ItemLine.Ranger, 7), 60);
            Assert.IsTrue(OutfitRules.Upgrades(ranged, windrunner, pieces));
            Assert.IsFalse(OutfitRules.Upgrades(mining, windrunner, pieces), "60 armor does not beat 62 where no line fits");
            var dragon = Piece("Dragon Chest", ItemType.Chest, ItemMaterial.Dragon, 66);
            Assert.IsFalse(OutfitRules.Upgrades(ranged, dragon, pieces), "a fitting piece counts as the plate of its level and a quarter");
            Assert.IsTrue(OutfitRules.Upgrades(mining, dragon, pieces));

            // two fitting lines of the same tier are the player's choice: the one in the outfit stays
            var hunter = Piece("Hunter Chest", ItemType.Chest, ItemLines.Material(ItemLine.WitchHunter, 6), 54);
            Assert.IsFalse(OutfitRules.Upgrades(ranged, hunter, pieces));

            // a role asked for with the outfit ("!equip tank" while training attack): only its own gear replaces it
            var tank = Set("Bulwark", ItemLines.Material(ItemLine.Tank, 7), 90, worn: false);
            var attack = OutfitOf(Skill.Attack, tank);
            attack.Word = "tank";
            var all = tank.ToList();
            Assert.IsFalse(OutfitRules.Upgrades(attack, Piece("Nightfall Chest", ItemType.Chest, ItemLines.Material(ItemLine.Rogue, 8), 60), all));
            Assert.IsFalse(OutfitRules.Upgrades(attack, Piece("Elder Atlarus Chest", ItemType.Chest, ItemMaterial.ElderAtlarus, 400), all));
            Assert.IsTrue(OutfitRules.Upgrades(attack, Piece("Aegis Chest", ItemType.Chest, ItemLines.Material(ItemLine.Tank, 8), 100), all));
        }

        [TestMethod]
        public void WhatIsWorn_IsOnlyReplacedByItself_WhenUpgradingIsOnAndTheSlotIsNotPinned()
        {
            Assert.IsTrue(OutfitRules.MayReplace(null, Skill.Attack, EquipmentSlot.Head), "no outfits: as the game always did");
            var outfits = new CharacterOutfits { Worn = Skill.Attack };
            Assert.IsTrue(OutfitRules.MayReplace(outfits, Skill.Attack, EquipmentSlot.Head));
            outfits.GetOrAdd(Skill.Attack).Set(EquipmentSlot.Head, Guid.NewGuid(), Guid.NewGuid(), true);
            outfits.GetOrAdd(Skill.Attack).Set(EquipmentSlot.Chest, Guid.NewGuid(), Guid.NewGuid(), false);
            Assert.IsFalse(OutfitRules.MayReplace(outfits, Skill.Attack, EquipmentSlot.Head));
            Assert.IsTrue(OutfitRules.MayReplace(outfits, Skill.Attack, EquipmentSlot.Chest));
            Assert.IsTrue(OutfitRules.MayReplace(outfits, Skill.Mining, EquipmentSlot.Head), "the pin is the attack outfit's");
            outfits.Upgrade = false;
            Assert.IsFalse(OutfitRules.MayReplace(outfits, Skill.Attack, EquipmentSlot.Chest));
            Assert.IsFalse(OutfitRules.MayReplace(outfits, Skill.Mining, EquipmentSlot.Head));
        }

        [TestMethod]
        public void Slots_AreTheSameOnBothSides()
        {
            Assert.AreEqual(EquipmentSlot.Head, OutfitRules.SlotOf(ItemType.Hat));
            Assert.AreEqual(EquipmentSlot.Head, OutfitRules.SlotOf(ItemType.HeadCovering));
            Assert.AreEqual(EquipmentSlot.MeleeWeapon, OutfitRules.SlotOf(ItemType.TwoHandedAxe));
            Assert.AreEqual(EquipmentSlot.RangedWeapon, OutfitRules.SlotOf(ItemType.TwoHandedBow));
            Assert.AreEqual(EquipmentSlot.MagicWeapon, OutfitRules.SlotOf(ItemType.TwoHandedStaff));
            Assert.AreEqual(EquipmentSlot.None, OutfitRules.SlotOf(ItemType.Food));
            // what the server's own mapping says, for every type of item
            foreach (ItemType type in Enum.GetValues(typeof(ItemType)))
            {
                var server = RavenNest.BusinessLogic.Game.ReadOnlyInventoryItem.GetEquipmentSlot((RavenNest.DataModels.ItemType)(int)type);
                Assert.AreEqual((int)server, (int)OutfitRules.SlotOf(type), type.ToString());
            }
            // pets, capes and boats are no part of an outfit
            Assert.IsFalse(OutfitRules.IsOutfitSlot(EquipmentSlot.Pet));
            Assert.IsFalse(OutfitRules.IsOutfitSlot(EquipmentSlot.Boat));
            Assert.IsFalse(OutfitRules.IsOutfitSlot(EquipmentSlot.None));
            Assert.AreEqual(11, OutfitRules.Slots.Length);
        }
    }
}
