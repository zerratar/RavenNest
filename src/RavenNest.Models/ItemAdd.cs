using System;

namespace RavenNest.Models
{
    public class ItemAdd
    {
        public string UserId { get; set; }
        public Guid ItemId { get; set; }
        public int Amount { get; set; }
    }
}