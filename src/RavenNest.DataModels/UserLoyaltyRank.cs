using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class UserLoyaltyRank : Entity<UserLoyaltyRank>
    {
        [PersistentData] private string title;
        [PersistentData] private string description;
        [PersistentData] private int levelRequirement;
    }
}
