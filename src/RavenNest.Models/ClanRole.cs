using System;

namespace RavenNest.Models
{
    public class ClanRole
    {
        public ClanRole() { }
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public int Cape { get; set; }

        /// <summary>This role's cape shape when it differs from the clan's (ClanCapeStyle).</summary>
        public int? CapeStyle { get; set; }
        public DateTime? Joined { get; set; }
    }
}
