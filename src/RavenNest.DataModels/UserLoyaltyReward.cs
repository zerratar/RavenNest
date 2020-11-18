using System;

namespace RavenNest.DataModels
{
    public class UserLoyaltyReward : Entity<UserLoyaltyReward>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid rankId; public Guid RankId { get => rankId; set => Set(ref rankId, value); }
        private int? points; public int? Points { get => points; set => Set(ref points, value); }
        private string rewardData; public string RewardData { get => rewardData; set => Set(ref rewardData, value); }
        private int rewardType; public int RewardType { get => rewardType; set => Set(ref rewardType, value); }
    }
}
