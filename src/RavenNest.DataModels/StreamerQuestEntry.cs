using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>A character's part in a streamer quest: its progress on a bounty, or what it added to a goal, and what it got.</summary>
    public partial class StreamerQuestEntry : Entity<StreamerQuestEntry>
    {
        [PersistentData] private Guid streamerQuestId;
        [PersistentData] private Guid characterId;
        [PersistentData] private long progress;
        /// <summary>When it claimed a bounty's reward.</summary>
        [PersistentData] private DateTime? completed;
        [PersistentData] private long coins;
        [PersistentData] private long items;
        [PersistentData] private long loyalty;
    }
}
