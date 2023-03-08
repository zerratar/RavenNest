using System;

namespace RavenNest.Models
{
    public class LoyaltyUpdate
    {
        public Guid PlayerId { get; set; }
        public string UserName { get; set; }
        public string UserId { get; set; }
        public int SubsCount { get; set; }
        public int BitsCount { get; set; }
        public DateTime Date { get; set; }
    }

}
