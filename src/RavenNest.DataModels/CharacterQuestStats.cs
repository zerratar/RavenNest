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

        /// <summary>
        ///     Days in a row the character played: in a running session at some point of each day (a long stream counts
        ///     every day it runs through, no rejoining needed). Pet eggs on day 7, then every 14 days.
        /// </summary>
        [PersistentData] private int playStreak;

        /// <summary>The last day the character played, "2026-11-09" (UTC).</summary>
        [PersistentData] private string lastPlayDay;
    }
}
