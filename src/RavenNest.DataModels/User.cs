﻿using System;

namespace RavenNest.DataModels
{
    public partial class User : Entity<User>
    {
        // code structure is a bit ugly here but
        // it is because it was auto generated with:

        // REGEX REPLACE: public ([a-zA-Z0-9|\?]+) ([a-zA-Z0-9]+) { get; set; }
        //          WITH: private $1 _$2; public $1 $2 { get => _$2; set => Set(ref _$2, value); }
        // To save some time. uggah

        [Obsolete("Use UserAccess.PlatformId")]
        private string userId; public string UserId { get => userId; set => Set(ref userId, value); }
        private string userName; public string UserName { get => userName; set => Set(ref userName, value); }
        private string displayName; public string DisplayName { get => displayName; set => Set(ref displayName, value); }
        private string email; public string Email { get => email; set => Set(ref email, value); }
        private string passwordHash; public string PasswordHash { get => passwordHash; set => Set(ref passwordHash, value); }
        private bool? isAdmin; public bool? IsAdmin { get => isAdmin; set => Set(ref isAdmin, value); }
        private bool? isModerator; public bool? IsModerator { get => isModerator; set => Set(ref isModerator, value); }
        private int? patreonTier; public int? PatreonTier { get => patreonTier; set => Set(ref patreonTier, value); }
        private int? status; public int? Status { get => status; set => Set(ref status, value); }
        private bool? isHiddenInHighscore; public bool? IsHiddenInHighscore { get => isHiddenInHighscore; set => Set(ref isHiddenInHighscore, value); }
        private DateTime? lastReward; public DateTime? LastReward { get => lastReward; set => Set(ref lastReward, value); }
        private DateTime created; public DateTime Created { get => created; set => Set(ref created, value); }
    }
}
