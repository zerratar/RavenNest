using System;

namespace RavenNest.DataModels
{
    public partial class PatreonSettings : Entity<PatreonSettings>
    {
        private string clientId; public string ClientId { get => clientId; set => Set(ref clientId, value); }
        private string clientSecret; public string ClientSecret { get => clientSecret; set => Set(ref clientSecret, value); }
        private string creatorAccessToken; public string CreatorAccessToken { get => creatorAccessToken; set => Set(ref creatorAccessToken, value); }
        private string creatorRefreshToken; public string CreatorRefreshToken { get => creatorRefreshToken; set => Set(ref creatorRefreshToken, value); }
        private DateTime lastUpdate; public DateTime LastUpdate { get => lastUpdate; set => Set(ref lastUpdate, value); }
        private string expiresIn; public string ExpiresIn { get => expiresIn; set => Set(ref expiresIn, value); }
        private string scope; public string Scope { get => scope; set => Set(ref scope, value); }
        private string tokenType; public string TokenType { get => tokenType; set => Set(ref tokenType, value); }
        private string webhookSecret; public string WebhookSecret { get => webhookSecret; set => Set(ref webhookSecret, value); }
    }
}
