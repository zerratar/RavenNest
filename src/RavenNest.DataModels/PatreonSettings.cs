using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class PatreonSettings : Entity<PatreonSettings>
    {
        [PersistentData] private string clientId;
        [PersistentData] private string clientSecret;
        [PersistentData] private string creatorAccessToken;
        [PersistentData] private string creatorRefreshToken;
        [PersistentData] private DateTime lastUpdate;
        [PersistentData] private string expiresIn;
        [PersistentData] private string scope;
        [PersistentData] private string tokenType;
        [PersistentData] private string webhookSecret;
    }
}
