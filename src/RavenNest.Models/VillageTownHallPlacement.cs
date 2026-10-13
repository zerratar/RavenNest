namespace RavenNest.Models
{
    /// <summary>
    /// The body of a town hall move: where to and how far turned. Kept apart from VillageTownHall, the
    /// answer type, on purpose: the game client sends its copy of that type with the tier fields left null,
    /// and System.Text.Json refuses null for an int, which dropped the whole request. Fields this type does
    /// not have are ignored.
    /// </summary>
    public class VillageTownHallPlacement
    {
        public double X { get; set; }
        public double Z { get; set; }
        public double Rotation { get; set; }
    }
}
