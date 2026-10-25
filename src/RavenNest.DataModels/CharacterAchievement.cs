using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     A character's progress on one achievement of AchievementManager's catalog: a lifetime count (trees cut,
    ///     raids won, ...) and the highest tier reached (0 none, 1 bronze, 2 silver, 3 gold). Reshaped 2026-10-25 from
    ///     the table that was never used.
    /// </summary>
    public partial class CharacterAchievement : Entity<CharacterAchievement>
    {
        /// <summary>Unused, from the old shape (definitions are in code now).</summary>
        [PersistentData] private Guid achievementId;
        [PersistentData] private Guid characterId;
        /// <summary>When the latest tier was reached (when the row was made, before that).</summary>
        [PersistentData] private DateTime achieved;
        /// <summary>The catalog key, "woodcutter".</summary>
        [PersistentData] private string key;
        [PersistentData] private long progress;
        [PersistentData] private int tier;
    }
}
