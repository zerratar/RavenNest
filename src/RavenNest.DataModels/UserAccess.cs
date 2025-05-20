using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class UserAccess : Entity<UserAccess>
    {
        [PersistentData] private Guid userId;
        [PersistentData] private string platformId;
        [PersistentData] private string platform;
        [PersistentData] private string platformUsername;
        [PersistentData] private string accessToken;
        [PersistentData] private string refreshToken;
        [PersistentData] private string profilePicture;
        [PersistentData] private string expiresIn;
        [PersistentData] private string scope;
        [PersistentData] private string tokenType;
        [PersistentData] private DateTime? updated;
        [PersistentData] private DateTime? created;
        [PersistentData] private DateTime? lastAccessTime;
    }
}
