using System;

namespace RavenNest.DataModels
{
    public partial class CharacterSessionActivity : Entity<CharacterSessionActivity>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid sessionId; public Guid SessionId { get => sessionId; set => Set(ref sessionId, value); }
        private Guid userId; public Guid UserId { get => userId; set => Set(ref userId, value); }
        private Guid characterId; public Guid CharacterId { get => characterId; set => Set(ref characterId, value); }
        private string userName; public string UserName { get => userName; set => Set(ref userName, value); }
        private string minResponseTime; public string MinResponseTime { get => minResponseTime; set => Set(ref minResponseTime, value); }
        private string maxResponseTime; public string MaxResponseTime { get => maxResponseTime; set => Set(ref maxResponseTime, value); }
        private string avgResponseTime; public string AvgResponseTime { get => avgResponseTime; set => Set(ref avgResponseTime, value); }
        private int responseStreak; public int ResponseStreak { get => responseStreak; set => Set(ref responseStreak, value); }
        private int maxResponseStreak; public int MaxResponseStreak { get => maxResponseStreak; set => Set(ref maxResponseStreak, value); }
        private int totalTriggerCount; public int TotalTriggerCount { get => totalTriggerCount; set => Set(ref totalTriggerCount, value); }
        private int totalInputCount; public int TotalInputCount { get => totalInputCount; set => Set(ref totalInputCount, value); }
        private int tripCount; public int TripCount { get => tripCount; set => Set(ref tripCount, value); }
        private bool tripped; public bool Tripped { get => tripped; set => Set(ref tripped, value); }
        private DateTime? updated; public DateTime? Updated { get => updated; set => Set(ref updated, value); }
        private DateTime? created; public DateTime? Created { get => created; set => Set(ref created, value); }
    }
}
