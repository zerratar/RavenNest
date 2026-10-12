using System;

namespace RavenNest.Models
{
    /// <summary>
    /// Where a village house has been placed in build mode. Kept apart from VillageHouseInfo on
    /// purpose: that type is compiled into every game client, and positions are only read by
    /// clients that know about build mode, through the village placements endpoint.
    /// </summary>
    public class VillageHousePlacement
    {
        public int Slot { get; set; }
        public double X { get; set; }
        public double Z { get; set; }
        public double Rotation { get; set; }
    }
}
