using System;

namespace RavenNest.DataModels
{
    public class UserClaimedLoyaltyReward : Entity<UserClaimedLoyaltyReward>
    {
        private Guid rewardId; public Guid RewardId { get => rewardId; set => Set(ref rewardId, value); }
        private Guid userId; public Guid UserId { get => userId; set => Set(ref userId, value); }
        private Guid? characterId; public Guid? CharacterId { get => characterId; set => Set(ref characterId, value); }
        private DateTime claimed; public DateTime Claimed { get => claimed; set => Set(ref claimed, value); }
    }
}
