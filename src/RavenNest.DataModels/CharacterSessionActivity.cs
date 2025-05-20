using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class CharacterSessionActivity : Entity<CharacterSessionActivity>
    {
        [PersistentData] private Guid sessionId;
        [PersistentData] private Guid userId;
        [PersistentData] private Guid characterId;
        [PersistentData] private string userName;
        [PersistentData] private string minResponseTime;
        [PersistentData] private string maxResponseTime;
        [PersistentData] private string avgResponseTime;
        [PersistentData] private int responseStreak;
        [PersistentData] private int maxResponseStreak;
        [PersistentData] private int totalTriggerCount;
        [PersistentData] private int totalInputCount;
        [PersistentData] private int tripCount;
        [PersistentData] private bool tripped;
        [PersistentData] private DateTime? updated;
        [PersistentData] private DateTime? created;
    }
}
