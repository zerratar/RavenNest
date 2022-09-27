using System;

namespace RavenNest.DataModels
{
    public class UserLoyaltyReward : Entity<UserLoyaltyReward>
    {
        private int? points; public int? Points { get => points; set => Set(ref points, value); }
        private string rewardData; public string RewardData { get => rewardData; set => Set(ref rewardData, value); }
        private int rewardType; public int RewardType { get => rewardType; set => Set(ref rewardType, value); }
    }
}
