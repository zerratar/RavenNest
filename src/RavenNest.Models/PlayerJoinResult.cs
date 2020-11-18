namespace RavenNest.Models
{
    public class PlayerJoinResult
    {
        public Player Player { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
}
