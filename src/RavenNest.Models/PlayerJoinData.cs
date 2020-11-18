namespace RavenNest.Models
{
    public class PlayerJoinData
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Identifier { get; set; }
        public bool Subscriber { get; set; }
        public bool Moderator { get; set; }
        public bool Vip { get; set; }
    }
}
