using System.Collections.Generic;

namespace RavenNest.Models
{
    public class RedeemableItemCollection : Collection<RedeemableItem>
    {
        public RedeemableItemCollection()
        {
        }

        public RedeemableItemCollection(IEnumerable<RedeemableItem> items)
            : base(items)
        {
        }
    }
}
