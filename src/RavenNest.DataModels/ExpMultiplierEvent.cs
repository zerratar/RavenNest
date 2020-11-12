using System;

namespace RavenNest.DataModels
{
    public partial class ExpMultiplierEvent : Entity<ExpMultiplierEvent>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private DateTime startTime; public DateTime StartTime { get => startTime; set => Set(ref startTime, value); }
        private DateTime endTime; public DateTime EndTime { get => endTime; set => Set(ref endTime, value); }
        private int multiplier; public int Multiplier { get => multiplier; set => Set(ref multiplier, value); }
        private string eventName; public string EventName { get => eventName; set => Set(ref eventName, value); }
    }
}
