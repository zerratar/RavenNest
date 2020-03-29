namespace RavenNest.Models
{
    public class PlayerNameUpdate
    {
        public string UserId { get; set; }
        public string Name { get; set; }
    }

    public class PlayerExpUpdate
    {
        public string UserId { get; set; }
        public string Skill { get; set; }
        public decimal Experience { get; set; }
    }
}