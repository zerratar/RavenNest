using System;

namespace RavenNest.DataModels
{
    public class UserPatreon : Entity<UserPatreon>
    {
        private Guid? userId; public Guid? UserId { get => userId; set => Set(ref userId, value); }
        private string twitchUserId; public string TwitchUserId { get => twitchUserId; set => Set(ref twitchUserId, value); }
        private string pledgeTitle; public string PledgeTitle { get => pledgeTitle; set => Set(ref pledgeTitle, value); }
        private string firstName; public string FirstName { get => firstName; set => Set(ref firstName, value); }
        private string fullName; public string FullName { get => fullName; set => Set(ref fullName, value); }
        private long? patreonId; public long? PatreonId { get => patreonId; set => Set(ref patreonId, value); }
        private long? pledgeAmount; public long? PledgeAmount { get => pledgeAmount; set => Set(ref pledgeAmount, value); }
        private string email; public string Email { get => email; set => Set(ref email, value); }
        private int? tier; public int? Tier { get => tier; set => Set(ref tier, value); }
        private string accessToken; public string AccessToken { get => accessToken; set => Set(ref accessToken, value); }
        private string refreshToken; public string RefreshToken { get => refreshToken; set => Set(ref refreshToken, value); }
        private string profilePicture; public string ProfilePicture { get => profilePicture; set => Set(ref profilePicture, value); }
        private string expiresIn; public string ExpiresIn { get => expiresIn; set => Set(ref expiresIn, value); }
        private string scope; public string Scope { get => scope; set => Set(ref scope, value); }
        private string tokenType; public string TokenType { get => tokenType; set => Set(ref tokenType, value); }
        private DateTime? updated; public DateTime? Updated { get => updated; set => Set(ref updated, value); }
        private DateTime? created; public DateTime? Created { get => created; set => Set(ref created, value); }
    }
}
