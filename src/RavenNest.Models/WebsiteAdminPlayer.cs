using System;

namespace RavenNest.Models
{
    public class WebsiteAdminPlayer : Player
    {
        public string PasswordHash { get; set; }
        public DateTime Created { get; set; }
        public string SessionName { get; set; }
    }
}
