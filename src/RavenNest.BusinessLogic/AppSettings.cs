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
        #region Twitch
        public string TwitchAccessToken { get; set; }
        public string TwitchRefreshToken { get; set; }
        public string TwitchClientId { get; set; }
        public string TwitchClientSecret { get; set; }
        public string OriginBroadcasterId { get; set; }
        #endregion

        public string DevelopmentServer { get; set; }

        #region Pocket full of Quarters
        public string PoQ_Dev_ClientId { get; set; }
        public string PoQ_Dev_ClientSecret { get; set; }
        public string PoQ_Dev_ServerAPIToken { get; set; }
        public string PoQ_Prod_ClientId { get; set; }
        public string PoQ_Prod_ClientSecret { get; set; }
        public string PoQ_Prod_ServerAPIToken { get; set; }
        public string PoQ_Dev_Url { get; set; }
        public string PoQ_Prod_Url { get; set; }
        #endregion

        #region Patreon
        public string PatreonDeleteMember { get; set; }
        public string PatreonUpdateMember { get; set; }
        public string PatreonCreateMember { get; set; }
        public string PatreonDeletePledge { get; set; }
        public string PatreonUpdatePledge { get; set; }
        public string PatreonCreatePledge { get; set; }
        #endregion
    }
}
