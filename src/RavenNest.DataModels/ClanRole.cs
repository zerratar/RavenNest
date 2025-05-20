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
    }
}
