using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class AchievementReward : Entity<Achievement>
    {
        [PersistentData] private Guid achievementId;
        [PersistentData] private int rewardType;
        [PersistentData] private long rewardAmount;
    }
}
