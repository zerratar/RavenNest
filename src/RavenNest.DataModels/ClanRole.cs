using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class ClanRole : Entity<ClanRole>
    {
        [PersistentData] private Guid clanId;
        [PersistentData] private int cape;
        [PersistentData] private int level;
        [PersistentData] private string name;

        // This role's cape shape when it differs from the clan's (Clan.CapeStyle); empty for the clan's.
        [PersistentData] private int? capeStyle;
    }
}
