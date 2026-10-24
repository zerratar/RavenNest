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

        /// <summary>The live custom logo (api/clan/image/{id}), empty for the owner's Twitch picture.</summary>
        public Guid? LogoImageId { get; set; }

        /// <summary>The live custom cape image, empty for the logo on a cloth colour.</summary>
        public Guid? CapeImageId { get; set; }

        /// <summary>The clan's cape shape (ClanCapeStyle).</summary>
        public int CapeStyle { get; set; }
        public ClanSkill[] ClanSkills { get; set; }
    }
}
