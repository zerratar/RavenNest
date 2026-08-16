using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.BusinessLogic.Game;

namespace RavenNest.UnitTests
{
    /// <summary>
    ///     StackKey replaced twelve pairwise CanBeStacked overloads with one definition, and the
    ///     whole value of that is only real if the definition says the same thing the twelve did.
    ///
    ///     The first test is the one that matters: it re-implements the old rule verbatim and
    ///     compares it against the new one over every combination of the fields that go into it.
    /// </summary>
    [TestClass]
    public class StackKeyTests
    {
        private static readonly Guid ItemA = new Guid("11111111-1111-1111-1111-111111111111");
        private static readonly Guid ItemB = new Guid("22222222-2222-2222-2222-222222222222");
        private static readonly Guid SkinA = new Guid("33333333-3333-3333-3333-333333333333");
        private static readonly Guid SkinB = new Guid("44444444-4444-4444-4444-444444444444");

        private static readonly Guid[] Items = { ItemA, ItemB };
        private static readonly string[] Tags = { null, "", "streamer-a", "streamer-b" };
        private static readonly string[] Enchantments = { null, "", "Strength 5", "Wisdom 3" };
        private static readonly Guid?[] Skins = { null, SkinA, SkinB };

        private static IEnumerable<RavenNest.DataModels.InventoryItem> AllStacks()
        {
            foreach (var itemId in Items)
                foreach (var tag in Tags)
                    foreach (var enchantment in Enchantments)
                        foreach (var skin in Skins)
                            yield return new RavenNest.DataModels.InventoryItem
                            {
                                Id = Guid.NewGuid(),
                                ItemId = itemId,
                                Tag = tag,
                                Enchantment = enchantment,
                                TransmogrificationId = skin
                            };
        }

        /// <summary>The rule as it was written before StackKey existed, copied verbatim.</summary>
        private static bool OldRule(
            RavenNest.DataModels.InventoryItem a, RavenNest.DataModels.InventoryItem b)
        {
            bool Stackable(RavenNest.DataModels.InventoryItem i) =>
                i != null && i.TransmogrificationId == null && string.IsNullOrEmpty(i.Enchantment);

            return Stackable(a) && Stackable(b) && a.Tag == b.Tag && a.ItemId == b.ItemId;
        }

        [TestMethod]
        public void MatchesTheRuleItReplaced()
        {
            var stacks = new List<RavenNest.DataModels.InventoryItem>(AllStacks());
            var compared = 0;

            foreach (var a in stacks)
            {
                foreach (var b in stacks)
                {
                    var expected = OldRule(a, b);
                    var actual = PlayerInventory.CanBeStacked(a, b);
                    Assert.AreEqual(expected, actual,
                        $"disagreed for a=[{a.Key()}] b=[{b.Key()}]");
                    compared++;
                }
            }

            // 2 items x 4 tags x 4 enchantments x 3 skins = 96 stacks, so 9216 pairs.
            Assert.AreEqual(9216, compared);
        }

        [TestMethod]
        public void NullsNeverStack()
        {
            var real = new RavenNest.DataModels.InventoryItem { ItemId = ItemA };

            Assert.IsFalse(PlayerInventory.CanBeStacked((RavenNest.DataModels.InventoryItem)null, real));
            Assert.IsFalse(PlayerInventory.CanBeStacked(real, (RavenNest.DataModels.InventoryItem)null));
            Assert.IsFalse(PlayerInventory.CanBeStacked(
                (RavenNest.DataModels.InventoryItem)null, (RavenNest.DataModels.InventoryItem)null));
        }

        /// <summary>
        ///     The one normalisation StackKey performs. Both null and "" mean "not enchanted" to
        ///     the rule it replaced, so they have to keep meaning the same thing here, or two
        ///     stacks that merge today would quietly stop merging.
        /// </summary>
        [TestMethod]
        public void EmptyAndNullEnchantmentAreTheSameThing()
        {
            var withNull = new StackKey(ItemA, null, null, null);
            var withEmpty = new StackKey(ItemA, null, "", null);

            Assert.AreEqual(withNull, withEmpty);
            Assert.AreEqual(withNull.GetHashCode(), withEmpty.GetHashCode());
            Assert.IsTrue(StackKey.CanMerge(withNull, withEmpty));
        }

        /// <summary>
        ///     And the one it deliberately does not perform, for the opposite reason: the old rule
        ///     compared tags with a plain ==, so these do not stack today and must not start.
        /// </summary>
        [TestMethod]
        public void EmptyAndNullTagAreNotTheSameThing()
        {
            var withNull = new StackKey(ItemA, null, null, null);
            var withEmpty = new StackKey(ItemA, "", null, null);

            Assert.AreNotEqual(withNull, withEmpty);
            Assert.IsFalse(StackKey.CanMerge(withNull, withEmpty));
        }

        [TestMethod]
        public void EnchantedAndSkinnedItemsNeverStack()
        {
            var plain = new StackKey(ItemA, null, null, null);
            var enchanted = new StackKey(ItemA, null, "Strength 5", null);
            var skinned = new StackKey(ItemA, null, null, SkinA);

            Assert.IsTrue(plain.IsStackable);
            Assert.IsFalse(enchanted.IsStackable);
            Assert.IsFalse(skinned.IsStackable);

            Assert.IsFalse(StackKey.CanMerge(enchanted, enchanted));
            Assert.IsFalse(StackKey.CanMerge(skinned, skinned));
            Assert.IsFalse(StackKey.CanMerge(plain, enchanted));
            Assert.IsFalse(StackKey.CanMerge(plain, skinned));
        }

        /// <summary>
        ///     Equality has to stay an equivalence relation even for keys that cannot merge, or
        ///     using StackKey as a dictionary key later would behave unpredictably. CanMerge is
        ///     the thing that is allowed to say no to two identical keys.
        /// </summary>
        [TestMethod]
        public void EqualityIsReflexiveEvenWhenUnstackable()
        {
            var enchanted = new StackKey(ItemA, "tag", "Strength 5", SkinA);
            var sameAgain = new StackKey(ItemA, "tag", "Strength 5", SkinA);

            Assert.AreEqual(enchanted, sameAgain);
            Assert.IsTrue(enchanted == sameAgain);
            Assert.AreEqual(enchanted.GetHashCode(), sameAgain.GetHashCode());
            Assert.IsFalse(StackKey.CanMerge(enchanted, sameAgain));
        }

        [TestMethod]
        public void DifferentItemsNeverStack()
        {
            Assert.IsFalse(StackKey.CanMerge(
                new StackKey(ItemA, null, null, null),
                new StackKey(ItemB, null, null, null)));
        }

        [TestMethod]
        public void DifferentTagsNeverStack()
        {
            Assert.IsFalse(StackKey.CanMerge(
                new StackKey(ItemA, "streamer-a", null, null),
                new StackKey(ItemA, "streamer-b", null, null)));
        }

        /// <summary>
        ///     Every representation of a stack has to project to the same key, since that is the
        ///     entire reason the type exists.
        /// </summary>
        [TestMethod]
        public void EveryRepresentationProjectsToTheSameKey()
        {
            var expected = new StackKey(ItemA, "streamer-a", null, null);

            var dataModel = new RavenNest.DataModels.InventoryItem
            {
                ItemId = ItemA,
                Tag = "streamer-a"
            };

            var apiModel = new RavenNest.Models.InventoryItem
            {
                ItemId = ItemA,
                Tag = "streamer-a"
            };

            var bankItem = new RavenNest.DataModels.UserBankItem
            {
                ItemId = ItemA,
                Tag = "streamer-a"
            };

            Assert.AreEqual(expected, dataModel.Key());
            Assert.AreEqual(expected, apiModel.Key());
            Assert.AreEqual(expected, bankItem.Key());

            Assert.IsTrue(PlayerInventory.CanBeStacked(dataModel, apiModel));
            Assert.IsTrue(PlayerInventory.CanBeStacked(dataModel, bankItem));
            Assert.IsTrue(PlayerInventory.CanBeStacked(bankItem, apiModel));
        }

        /// <summary>
        ///     An add request carries only an item id, so its key is always tagless and always
        ///     stackable. That is a property of the type rather than a rule, and the pairwise
        ///     overload for it is left matching on the id alone for the same reason.
        /// </summary>
        [TestMethod]
        public void AddRequestKeyIsJustTheItemId()
        {
            var request = new RavenNest.Models.AddItemRequest { ItemId = ItemA };

            Assert.AreEqual(new StackKey(ItemA, null, null, null), request.Key());
            Assert.IsTrue(request.Key().IsStackable);

            var tagged = new RavenNest.DataModels.InventoryItem { ItemId = ItemA, Tag = "streamer-a" };
            Assert.IsTrue(PlayerInventory.CanBeStacked(tagged, request));

            var enchanted = new RavenNest.DataModels.InventoryItem { ItemId = ItemA, Enchantment = "Strength 5" };
            Assert.IsFalse(PlayerInventory.CanBeStacked(enchanted, request));
        }
    }
}
