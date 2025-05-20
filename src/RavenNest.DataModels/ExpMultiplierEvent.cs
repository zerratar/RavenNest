using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class ExpMultiplierEvent : Entity<ExpMultiplierEvent>
    {
        [PersistentData] private DateTime startTime;
        [PersistentData] private DateTime endTime;
        [PersistentData] private int multiplier;
        [PersistentData] private string eventName;
        [PersistentData] private bool startedByPlayer;
    }
}
