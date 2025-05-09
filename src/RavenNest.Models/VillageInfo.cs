using System.Collections.Generic;

namespace RavenNest.Models
{
    public class VillageInfo
    {
        public int Level { get; set; }
        public long Experience { get; set; }
        public string Name { get; set; }
        public long Coins { get; set; }
        public long Wood { get; set; }
        public long Ore { get; set; }
        public long Wheat { get; set; }
        public long Fish { get; set; }
        public IReadOnlyList<VillageHouseInfo> Houses { get; set; }
    }
}
