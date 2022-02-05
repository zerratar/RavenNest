using System;

namespace RavenNest.BusinessLogic.Net
{
    [Obsolete("Use LoyaltyUpdate instead")]
    public class UserLoyaltyUpdate
    {
        public Guid CharacterId { get; set; }
        public string UserId { get; set; }
        public bool IsModerator { get; set; }
        public bool IsSubscriber { get; set; }
        public bool IsVip { get; set; }
        public int NewGiftedSubs { get; set; }
        public int NewCheeredBits { get; set; }
        public string UserName { get; set; }
    }

}
