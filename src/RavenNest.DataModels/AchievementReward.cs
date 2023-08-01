using System;

namespace RavenNest.DataModels
{
    public partial class AchievementReward : Entity<Achievement>
    {
        private Guid achievementId; public Guid AchievementId { get => achievementId; set => Set(ref achievementId, value); }
        private int rewardType; public int RewardType { get => rewardType; set => Set(ref rewardType, value); }
        private long rewardAmount; public long RewardAmount { get => rewardAmount; set => Set(ref rewardAmount, value); }
    }
}
