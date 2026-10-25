using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>A character's quest record: the daily streak, how many quests it has finished, and the title it wears.</summary>
    public partial class CharacterQuestStats : Entity<CharacterQuestStats>
    {
        [PersistentData] private Guid characterId;

        /// <summary>Days in a row with a daily quest finished.</summary>
        [PersistentData] private int dailyStreak;

        /// <summary>The last day a daily quest was finished, "2026-10-25".</summary>
        [PersistentData] private string lastDailyDay;
        [PersistentData] private long dailiesCompleted;
        [PersistentData] private long weekliesCompleted;
        /// <summary>The title shown on the name tag ("woodcutter_2", "cat"), or empty.</summary>
        [PersistentData] private string title;
    }
}
