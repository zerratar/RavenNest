using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public class WebsiteAdminUser
    {
        public Guid Id { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public int? PatreonTier { get; set; }
        public string Email { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsModerator { get; set; }
        public List<WebsiteAdminPlayer> Characters { get; set; }
        public int Status { get; set; }
            
        public DateTime Created { get; set; }
    }
}
