namespace RavenNest.Models
{
    public class PlayerRestoreResult
    {
        public PlayerJoinResult[] Players { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
}
