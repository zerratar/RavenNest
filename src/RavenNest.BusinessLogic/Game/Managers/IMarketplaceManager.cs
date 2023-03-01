using System;
using System.Threading.Tasks;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IMarketplaceManager
    {
        ItemSellResult SellItem(SessionToken token, string userId, Guid itemId, long amount, double pricePerItem);
        ItemBuyResult BuyItem(SessionToken token, string userId, Guid itemId, long amount, double maxPricePerItem);
        MarketItemCollection GetItems(int offset, int size);
        MarketItemCollection GetItems(ItemFilter filter, int offset, int size);
        bool Cancel(Guid id);
    }
}
