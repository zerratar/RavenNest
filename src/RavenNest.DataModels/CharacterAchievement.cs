using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class CharacterAchievement : Entity<CharacterAchievement>
    {
        [PersistentData] private Guid achievementId;
        [PersistentData] private Guid characterId;
        [PersistentData] private DateTime achieved;
    }
}
