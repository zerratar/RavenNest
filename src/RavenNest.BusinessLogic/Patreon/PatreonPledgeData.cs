namespace RavenNest.BusinessLogic.Patreon
{
    public class PatreonPledgeData : IPatreonData
    {
        public long PatreonId { get; set; }
        public string TwitchUserId { get; set; }
        public long PledgeAmountCents { get; set; }
        public string FullName { get; set; }
        public string FirstName { get; set; }
        public string Email { get; set; }
        public string RewardTitle { get; set; }
        public int Tier { get; set; }
    }
}
