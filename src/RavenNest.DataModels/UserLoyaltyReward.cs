using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class UserLoyaltyReward : Entity<UserLoyaltyReward>
    {
        [PersistentData] private int? points;
        [PersistentData] private string rewardData;
        [PersistentData] private int rewardType;
    }
}
