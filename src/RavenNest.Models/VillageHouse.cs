using System;

namespace RavenNest.Models
{
    public class VillageHouse
    {
        public Guid Id { get; set; }
        public string Owner { get; set; }
        public int Rank { get; set; }
        public int Type { get; set; }
        public int Slot { get; set; }
        public DateTime Created { get; set; }
    }
}
