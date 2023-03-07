using System;

namespace RavenNest.DataModels
{
    public partial class UserAccess : Entity<UserAccess>
    {
        private Guid userId; public Guid UserId { get => userId; set => Set(ref userId, value); }
        private string platformId; public string PlatformId { get => platformId; set => Set(ref platformId, value); }
        private string platform; public string Platform { get => platform; set => Set(ref platform, value); }
        private string platformUsername; public string PlatformUsername { get => platformUsername; set => Set(ref platformUsername, value); }
        private string accessToken; public string AccessToken { get => accessToken; set => Set(ref accessToken, value); }
        private string refreshToken; public string RefreshToken { get => refreshToken; set => Set(ref refreshToken, value); }
        private string profilePicture; public string ProfilePicture { get => profilePicture; set => Set(ref profilePicture, value); }
        private string expiresIn; public string ExpiresIn { get => expiresIn; set => Set(ref expiresIn, value); }
        private string scope; public string Scope { get => scope; set => Set(ref scope, value); }
        private string tokenType; public string TokenType { get => tokenType; set => Set(ref tokenType, value); }
        private DateTime? updated; public DateTime? Updated { get => updated; set => Set(ref updated, value); }
        private DateTime? created; public DateTime? Created { get => created; set => Set(ref created, value); }
        private DateTime? lastAccessTime; public DateTime? LastAccessTime { get => lastAccessTime; set => Set(ref lastAccessTime, value); }

    }
}
