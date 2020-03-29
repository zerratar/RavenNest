namespace RavenNest.Models
{
    public class GameSessionPlayer
    {
        public string TwitchUserId { get; set; }
        public string UserName { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsModerator { get; set; }
    }
}