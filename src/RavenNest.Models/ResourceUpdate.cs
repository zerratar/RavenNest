namespace RavenNest.Models
{
    public class ResourceUpdate
    {
        public string UserId { get; set; }
        public decimal WoodAmount { get; set; }
        public decimal OreAmount { get; set; }
        public decimal WheatAmount { get; set; }
        public decimal FishAmount { get; set; }
        public decimal CoinsAmount { get; set; }
    }
}