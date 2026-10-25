using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     The loyalty points a streamer has handed out through their quests in a week. Each week brings the same
    ///     allowance (StreamerQuestManager.WeeklyLoyaltyBudget); what is not used does not carry over.
    /// </summary>
    public partial class StreamerQuestBudget : Entity<StreamerQuestBudget>
    {
        [PersistentData] private Guid streamerUserId;
        /// <summary>"2026-W43".</summary>
        [PersistentData] private string week;
        [PersistentData] private long spent;
    }
}
