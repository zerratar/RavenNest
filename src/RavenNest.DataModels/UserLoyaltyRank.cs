using System;

namespace RavenNest.DataModels
{
    public class UserLoyaltyRank : Entity<UserLoyaltyRank>
    {
        private string title; public string Title { get => title; set => Set(ref title, value); }
        private string description; public string Description { get => description; set => Set(ref description, value); }
        private int levelRequirement; public int LevelRequirement { get => levelRequirement; set => Set(ref levelRequirement, value); }
    }
}
