using System;
using System.Threading.Tasks;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IMarketplaceManager
    {
        ItemSellResult SellItem(SessionToken token, string userId, Guid itemId, long amount, decimal pricePerItem);
        ItemBuyResult BuyItem(SessionToken token, string userId, Guid itemId, long amount, decimal maxPricePerItem);
        MarketItemCollection GetItems(int offset, int size);
    }
}