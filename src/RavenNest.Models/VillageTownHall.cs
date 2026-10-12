namespace RavenNest.Models
{
    /// <summary>
    /// Where a village's town hall stands, if the streamer has moved it in build mode. Kept apart from
    /// VillageInfo on purpose: that type is compiled into every game client, and only clients that know
    /// about build mode read this, through the village town hall endpoint. Null X and Z mean the plaza.
    /// </summary>
    public class VillageTownHall
    {
        public double? X { get; set; }
        public double? Z { get; set; }
        public double Rotation { get; set; }

        /// <summary>The tier the village has, 0 (the longhouse) to MaxTier.</summary>
        public int Tier { get; set; }
        public int MaxTier { get; set; }

        /// <summary>What the next tier needs. All zero when the village has the last tier.</summary>
        public int NextLevel { get; set; }
        public int NextWood { get; set; }
        public int NextOre { get; set; }
        public int NextFish { get; set; }
        public int NextWheat { get; set; }
    }
}
