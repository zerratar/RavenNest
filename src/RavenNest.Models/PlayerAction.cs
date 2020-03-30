namespace RavenNest.Models
{
    public class PlayerAction
    {
        public string UserId { get; set; }
        public string TargetId { get; set; }
        public int ActionType { get; set; }
    }
}