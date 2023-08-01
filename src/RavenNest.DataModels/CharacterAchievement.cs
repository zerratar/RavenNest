using System;

namespace RavenNest.DataModels
{
    public partial class CharacterAchievement : Entity<CharacterAchievement>
    {
        private Guid achievementId; public Guid AchievementId { get => achievementId; set => Set(ref achievementId, value); }
        private Guid characterId; public Guid CharacterId { get => characterId; set => Set(ref characterId, value); }
        private DateTime achieved; public DateTime Achieved { get => achieved; set => Set(ref achieved, value); }
    }
}
