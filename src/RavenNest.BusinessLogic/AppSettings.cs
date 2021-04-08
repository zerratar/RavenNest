namespace RavenNest.BusinessLogic
{
    public class AzureAppSettings
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string KeyIdentifier { get; set; }
    }

    public class AppSettings
    {
        public string DbConnectionString { get; set; }
        public string TwitchAccessToken { get; set; }
        public string TwitchRefreshToken { get; set; }
        public string TwitchClientId { get; set; }
        public string TwitchClientSecret { get; set; }
        public string OriginBroadcasterId { get; set; }
        public string DevelopmentServer { get; set; }
        public string PatreonDeleteMember { get; set; }
        public string PatreonUpdateMember { get; set; }
        public string PatreonCreateMember { get; set; }
        public string PatreonDeletePledge { get; set; }
        public string PatreonUpdatePledge { get; set; }
        public string PatreonCreatePledge { get; set; }
    }
}
