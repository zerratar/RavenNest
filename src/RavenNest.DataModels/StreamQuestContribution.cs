using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>What one character added to a stream quest, and the coins it got for it.</summary>
    public partial class StreamQuestContribution : Entity<StreamQuestContribution>
    {
        [PersistentData] private Guid streamQuestId;
        [PersistentData] private Guid characterId;
        [PersistentData] private long amount;
        [PersistentData] private long coins;
    }
}
