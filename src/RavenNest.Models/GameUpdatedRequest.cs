namespace RavenNest.Models
{
    public class GameUpdatedRequest
    {
        public string ExpectedVersion { get; set; }
        public bool UpdateRequired { get; set; }
    }
}
