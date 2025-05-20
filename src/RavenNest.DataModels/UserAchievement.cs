using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class UserAchievement : Entity<UserAchievement>
    {
        [PersistentData] private Guid achievementId;
        [PersistentData] private Guid userId;
        [PersistentData] private DateTime achieved;
    }
}
