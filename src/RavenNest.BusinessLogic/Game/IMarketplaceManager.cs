using System;
using System.Threading.Tasks;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IMarketplaceManager
    {
        Task<ItemSellResult> SellItemAsync(SessionToken token, string userId, Guid itemId, long amount, decimal pricePerItem);
        Task<ItemBuyResult> BuyItemAsync(SessionToken token, string userId, Guid itemId, long amount, decimal maxPricePerItem);
        Task<MarketItemCollection> GetItemsAsync(int offset, int size);
    }
}