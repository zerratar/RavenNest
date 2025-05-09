using System;

namespace RavenNest.Models
{
    public class VillageHouseInfo
    {
        public string Owner { get; set; }
        public Guid? OwnerCharacterId { get; set; }
        public Guid? OwnerUserId { get; set; }
        public int Type { get; set; }
        public int Slot { get; set; }
    }
}
