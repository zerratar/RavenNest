using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class UserClaimedLoyaltyReward : Entity<UserClaimedLoyaltyReward>
    {
        [PersistentData] private Guid rewardId;
        [PersistentData] private Guid userId;
        [PersistentData] private Guid? characterId;
        [PersistentData] private DateTime claimed;
    }
}
