using System;

namespace RavenNest.DataModels
{
    public partial class UserAchievement : Entity<UserAchievement>
    {
        private Guid achievementId; public Guid AchievementId { get => achievementId; set => Set(ref achievementId, value); }
        private Guid userId; public Guid UserId { get => userId; set => Set(ref userId, value); }
        private DateTime achieved; public DateTime Achieved { get => achieved; set => Set(ref achieved, value); }
    }
}
