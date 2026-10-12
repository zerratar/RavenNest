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
    }
}
