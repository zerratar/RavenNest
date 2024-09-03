using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public class ItemRemove
    {
        public Guid PlayerId { get; set; }
        public Guid InventoryItemId { get; set; }
        public Guid ItemId { get; set; }
        public long Amount { get; set; }
    }

    public class ItemRemoveByCategory
    {
        public Guid PlayerId { get; set; }
        public ItemFilter Filter { get; set; }
        public IReadOnlyList<Guid> Exclude { get; set; }
    }

    public enum ItemFilter
    {
        All,
        Swords,
        Bows,
        Staves,
        Shields,
        Armors,
        Accessories,
        Pets,
        Resources,

        Crafting,
        Cooking,
        Alchemy,

        Farming,
        Fishing,
        Gathering,
        Woodcutting,
        Mining,

        Food,
        Potions,
        Scrolls,

        Axes,
        Spears
    }

    public static class ItemFilterExtensions
    {
        public static ItemFilter GetItemFilter(this Item item)
        {
            return GetItemFilter(item.Category, item.Type);
        }

        public static ItemFilter GetItemFilter(ItemCategory itemCategory, ItemType itemType)
        {
            if (itemType == ItemType.Coins) return ItemFilter.Resources;
            if (itemType == ItemType.Mining) return ItemFilter.Mining;
            if (itemType == ItemType.Woodcutting) return ItemFilter.Woodcutting;
            if (itemType == ItemType.Gathering) return ItemFilter.Gathering;
            if (itemType == ItemType.Fishing) return ItemFilter.Fishing;
            if (itemType == ItemType.Farming) return ItemFilter.Farming;
            if (itemType == ItemType.Crafting) return ItemFilter.Crafting;
            if (itemType == ItemType.Cooking || itemType == ItemType.Food || itemCategory == ItemCategory.Food)
                return ItemFilter.Cooking;

            if (itemType == ItemType.Alchemy || itemType == ItemType.Potion || itemCategory == ItemCategory.Potion)
                return ItemFilter.Alchemy;

            if (itemType == ItemType.OneHandedSword || itemType == ItemType.TwoHandedSword)
                return ItemFilter.Swords;
            if (itemType == ItemType.TwoHandedBow) return ItemFilter.Bows;
            if (itemType == ItemType.TwoHandedStaff) return ItemFilter.Staves;
            if (itemType == ItemType.TwoHandedSpear) return ItemFilter.Spears;
            if (itemType == ItemType.OneHandedAxe || itemType == ItemType.TwoHandedAxe) return ItemFilter.Axes;
            if (itemType == ItemType.Ring || itemType == ItemType.Amulet) return ItemFilter.Accessories;
            if (itemType == ItemType.Shield) return ItemFilter.Shields;
            if (itemType == ItemType.Pet) return ItemFilter.Pets;
            if (itemType == ItemType.Scroll) return ItemFilter.Scrolls;

            if (itemCategory == ItemCategory.Armor || itemCategory == ItemCategory.Cosmetic)
                return ItemFilter.Armors;

            return ItemFilter.All;
        }
    }
}
