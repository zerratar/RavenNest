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
        public string PoQClientId { get; set; }
        public string PoQClientSecret { get; set; }
        public string PoQServerAPIToken { get; set; }
        public string PoQProdUrl { get; set; }
        public string PoQDevUrl { get; set; }
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
