using System;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    ///     One answer to "are these the same stack".
    ///
    ///     There are five representations of an item stack in this codebase:
    ///     <see cref="RavenNest.DataModels.InventoryItem"/>,
    ///     <see cref="RavenNest.Models.InventoryItem"/>, <see cref="ReadOnlyInventoryItem"/>,
    ///     <see cref="RavenNest.DataModels.UserBankItem"/> and
    ///     <see cref="RavenNest.Models.AddItemRequest"/>. Anything that compared two of them needed
    ///     a pairwise overload, so the count grew with the square of the representations: twelve
    ///     CanBeStacked overloads for five types. Every one of them said the same thing.
    ///
    ///     Each representation projects to this instead, so there is one definition to read and one
    ///     place to change if the rule ever moves.
    /// </summary>
    /// <remarks>
    ///     This reproduces the existing rule exactly rather than improving it. Two things are
    ///     deliberately absent, because the pairwise overloads never compared them either:
    ///
    ///     <list type="bullet">
    ///     <item><description>
    ///         <c>Name</c>. Two stacks of the same item with different names merge today, and the
    ///         surviving stack keeps the name of whichever was there first. Names on stackable
    ///         items come from market listings, so it is rare and low harm, but it is a real
    ///         difference and adding Name to the key is the fix if it ever matters.
    ///     </description></item>
    ///     <item><description>
    ///         <c>Flags</c>. Same reasoning.
    ///     </description></item>
    ///     </list>
    /// </remarks>
    public readonly struct StackKey : IEquatable<StackKey>
    {
        public readonly Guid ItemId;
        public readonly string Tag;
        public readonly string Enchantment;
        public readonly Guid? TransmogrificationId;

        public StackKey(Guid itemId, string tag, string enchantment, Guid? transmogrificationId)
        {
            ItemId = itemId;

            // Tag is not normalised. The rule this replaces compared tags with a plain ==, so a
            // null tag and an empty one do not stack today, and making them stack here would be a
            // behaviour change smuggled in under a refactor.
            Tag = tag;

            // Enchantment is normalised, for the opposite reason. The rule tested it with
            // IsNullOrEmpty, so null and "" both already mean "not enchanted", and leaving them
            // distinct here would stop two stacks merging that merge today.
            Enchantment = string.IsNullOrEmpty(enchantment) ? null : enchantment;

            TransmogrificationId = transmogrificationId;
        }

        /// <summary>
        ///     Whether a stack with this key can hold more than one of the item at all.
        ///
        ///     An enchantment or a transmogrification belongs to one specific item, so two of them
        ///     can never be the same thing however identical the rest of the key is.
        /// </summary>
        public bool IsStackable => TransmogrificationId == null && Enchantment == null;

        /// <summary>
        ///     Whether two stacks can merge. Both have to be stackable at all, and then they have
        ///     to be the same stack.
        /// </summary>
        public static bool CanMerge(StackKey a, StackKey b)
        {
            return a.IsStackable && b.IsStackable && a.Equals(b);
        }

        public bool Equals(StackKey other)
        {
            return ItemId == other.ItemId
                && TransmogrificationId == other.TransmogrificationId
                && string.Equals(Tag, other.Tag, StringComparison.Ordinal)
                && string.Equals(Enchantment, other.Enchantment, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is StackKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(ItemId, Tag, Enchantment, TransmogrificationId);

        public static bool operator ==(StackKey a, StackKey b) => a.Equals(b);

        public static bool operator !=(StackKey a, StackKey b) => !a.Equals(b);

        public override string ToString()
        {
            var text = ItemId.ToString();
            if (!string.IsNullOrEmpty(Tag)) text += " tag:" + Tag;
            if (Enchantment != null) text += " ench:" + Enchantment;
            if (TransmogrificationId != null) text += " skin:" + TransmogrificationId;
            return text;
        }
    }

    /// <summary>
    ///     The projections. One per representation, which is five methods where the pairwise
    ///     comparisons were twelve.
    /// </summary>
    public static class StackKeys
    {
        public static StackKey Key(this DataModels.InventoryItem item)
        {
            return new StackKey(item.ItemId, item.Tag, item.Enchantment, item.TransmogrificationId);
        }

        public static StackKey Key(this DataModels.UserBankItem item)
        {
            return new StackKey(item.ItemId, item.Tag, item.Enchantment, item.TransmogrificationId);
        }

        public static StackKey Key(this RavenNest.Models.InventoryItem item)
        {
            return new StackKey(item.ItemId, item.Tag, item.Enchantment, item.TransmogrificationId);
        }

        public static StackKey Key(this ReadOnlyInventoryItem item)
        {
            return new StackKey(item.ItemId, item.Tag, item.Enchantment, item.TransmogrificationId);
        }

        /// <summary>
        ///     An add request carries an item id and nothing else: no tag, no enchantment, no
        ///     transmogrification. So its key is always stackable and always tagless, which is why
        ///     the pairwise rule for it never tested either of those. That is a property of the
        ///     type rather than an oversight.
        /// </summary>
        public static StackKey Key(this RavenNest.Models.AddItemRequest item)
        {
            return new StackKey(item.ItemId, null, null, null);
        }
    }
}
