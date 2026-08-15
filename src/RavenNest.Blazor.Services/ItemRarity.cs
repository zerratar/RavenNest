namespace RavenNest.Blazor.Services
{
    public enum ItemRarityBand
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Mythic
    }

    /// <summary>
    ///     Ravenfall has no rarity field. What it has is <see cref="RavenNest.Models.ItemMaterial"/>,
    ///     declared in ascending order of quality, and the site already treats that order as the
    ///     ranking: sorting a stash by material sorts by the same index this bands on.
    ///
    ///     Six bands rather than thirty materials, because the point of colour here is to let a
    ///     player pick the good thing out of a long list at a glance. Thirty shades is a legend to
    ///     memorise; six is a glance.
    ///
    ///     The colours themselves are the rarity tokens in ravenfall-tokens.css, which were defined
    ///     when the design system was built and had nothing using them until now.
    /// </summary>
    public static class ItemRarity
    {
        // Bronze and Iron are what everything drops; the top of the plain run is Atlarus, and the
        // Elder materials sit above all of it, which is why they get a band to themselves.
        private const int LastCommon = 2;      // None, Bronze, Iron
        private const int LastUncommon = 5;    // Steel, Black, Mithril
        private const int LastRare = 8;        // Adamantite, Rune, Dragon
        private const int LastEpic = 11;       // Abraxas, Phantom, Lionsbane
        private const int LastLegendary = 14;  // Ether, Ancient, Atlarus

        public static ItemRarityBand FromMaterialIndex(int materialIndex)
        {
            if (materialIndex <= LastCommon) return ItemRarityBand.Common;
            if (materialIndex <= LastUncommon) return ItemRarityBand.Uncommon;
            if (materialIndex <= LastRare) return ItemRarityBand.Rare;
            if (materialIndex <= LastEpic) return ItemRarityBand.Epic;
            if (materialIndex <= LastLegendary) return ItemRarityBand.Legendary;
            return ItemRarityBand.Mythic;
        }

        public static string CssClass(ItemRarityBand band)
        {
            return "rf-rarity-" + band.ToString().ToLowerInvariant();
        }

        public static string Name(ItemRarityBand band)
        {
            return band.ToString();
        }
    }
}
