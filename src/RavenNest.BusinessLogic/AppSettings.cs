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
        public string TwitchClientId { get; set; }
        public string TwitchClientSecret { get; set; }
    }
}