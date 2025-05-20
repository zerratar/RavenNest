using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class ClanSkill : Entity<ClanSkill>
    {
        [PersistentData] private Guid clanId;
        [PersistentData] private Guid skillId;
        [PersistentData] private int level;
        [PersistentData] private double experience;
    }
}
