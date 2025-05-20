using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class User : Entity<User>
    {
        [PersistentData] private string userId;
        [PersistentData] private string userName;
        [PersistentData] private string displayName;
        [PersistentData] private string email;
        [PersistentData] private string passwordHash;
        [PersistentData] private bool? isAdmin;
        [PersistentData] private bool? isModerator;
        [PersistentData] private int? patreonTier;
        [PersistentData] private int? status;
        [PersistentData] private bool? isHiddenInHighscore;
        [PersistentData] private Guid? resources;
        [PersistentData] private DateTime? lastReward;
        [PersistentData] private DateTime created;
    }
}
