using System;

namespace RavenNest.Models
{
    public class Clan
    {
        public Clan() { }
        public Guid Id { get; set; }
        [Obsolete("Use Owner User Id instead")]
        public string Owner { get; set; }
        public Guid OwnerUserId { get; set; }
        public int Level { get; set; }
        public double Experience { get; set; }
        public string Name { get; set; }
        public string Logo { get; set; }
        public ClanSkill[] ClanSkills { get; set; }
    }
}
