using System;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Data
{
    public partial class GameData
    {
        /// <summary>
        ///     The Treasure Map a Message in a Bottle sometimes holds (BottleManager): a scroll, so the game lets it be used;
        ///     bound to its finder, since the island it marks lives in the stack's tag (no trading, gifting, marketplace or
        ///     clan bank), and worth a coin at the vendor, as the bottle is (ApplyVendorPrices' least). Never crafted.
        /// </summary>
        private void EnsureTreasureMap(TypedItems items)
        {
            var map = items.TreasureMap;
            if (map == null) return;
            if (!map.Soulbound || map.ShopSellPrice > 1 || map.ShopBuyPrice != 0 || map.Craftable)
            {
                map.Soulbound = true;
                map.ShopSellPrice = 1;
                map.ShopBuyPrice = 0;
                map.Craftable = false;
                map.Modified = DateTime.UtcNow;
            }
        }

        /// <summary>
        ///     The odd finds (OddFinds: an Old Boot, a Pet Rock and the rest): junk worth a coin at the vendor, never sold
        ///     in a shop or crafted. Tradable, so a Pet Rock can be given to a friend.
        /// </summary>
        private void EnsureOddFinds(TypedItems items)
        {
            foreach (var name in RavenNest.BusinessLogic.Game.OddFinds.Names)
            {
                var item = items.OddFind(name);
                if (item == null) continue;
                if (item.ShopSellPrice != 1 || item.ShopBuyPrice != 0 || item.Craftable)
                {
                    item.ShopSellPrice = 1;
                    item.ShopBuyPrice = 0;
                    item.Craftable = false;
                    item.Modified = DateTime.UtcNow;
                }
            }
        }
    }
}
