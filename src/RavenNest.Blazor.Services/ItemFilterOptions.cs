using RavenNest.Models;

namespace RavenNest.Blazor.Services
{
    /// <summary>One filter button. Order here is the order on screen.</summary>
    public sealed record ItemFilterOption(ItemFilter Filter, string Label, string Icon);

    /// <summary>
    ///     The category rail, in one place.
    ///
    ///     It used to be nineteen hand written blocks in the stash page and nineteen more in
    ///     PlayerInventory, which is how the two ended up with different labels ("Armor" against
    ///     "Armors"), different icons for alchemy, and no way to add a category without editing
    ///     both. Adding one is now a line here.
    /// </summary>
    public static class ItemFilterOptions
    {
        // Grouped the way a player thinks about their items: everything, then things you fight
        // with, then things you wear, then everything a skill produced.
        public static readonly ItemFilterOption[] All = new[]
        {
            new ItemFilterOption(ItemFilter.All, "All", "fa-sharp fa-solid fa-rectangles-mixed"),

            new ItemFilterOption(ItemFilter.Swords, "Swords", "fa-sharp fa-solid fa-sword"),
            new ItemFilterOption(ItemFilter.Axes, "Axes", "fa-sharp fa-solid fa-axe"),
            new ItemFilterOption(ItemFilter.Spears, "Spears", "fa-solid fa-scythe"),
            new ItemFilterOption(ItemFilter.Bows, "Bows", "fa-sharp fa-solid fa-bow-arrow"),
            new ItemFilterOption(ItemFilter.Staves, "Staves", "fa-sharp fa-solid fa-staff"),
            new ItemFilterOption(ItemFilter.Shields, "Shields", "fa-sharp fa-solid fa-shield"),

            new ItemFilterOption(ItemFilter.Armors, "Armor", "fa-sharp fa-solid fa-helmet-battle"),
            new ItemFilterOption(ItemFilter.Accessories, "Accessories", "fa-sharp fa-solid fa-gem"),
            new ItemFilterOption(ItemFilter.Pets, "Pets", "fa-sharp fa-solid fa-dog"),
            new ItemFilterOption(ItemFilter.Scrolls, "Scrolls", "fa-sharp fa-solid fa-scroll"),

            new ItemFilterOption(ItemFilter.Woodcutting, "Woodcutting", "fa-solid fa-tree"),
            new ItemFilterOption(ItemFilter.Mining, "Mining", "fa-solid fa-pickaxe"),
            new ItemFilterOption(ItemFilter.Fishing, "Fishing", "fa-sharp fa-solid fa-fishing-rod"),
            new ItemFilterOption(ItemFilter.Farming, "Farming", "fa-solid fa-wheat"),
            new ItemFilterOption(ItemFilter.Gathering, "Gathering", "fa-solid fa-mushroom"),
            new ItemFilterOption(ItemFilter.Crafting, "Crafting", "fa-sharp fa-solid fa-hammer"),
            new ItemFilterOption(ItemFilter.Cooking, "Cooking", "fa-sharp fa-solid fa-user-chef"),
            new ItemFilterOption(ItemFilter.Alchemy, "Alchemy", "fa-sharp fa-solid fa-flask"),
        };
    }
}
