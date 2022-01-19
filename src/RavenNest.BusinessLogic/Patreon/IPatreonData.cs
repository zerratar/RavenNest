namespace RavenNest.BusinessLogic.Patreon
{
    public interface IPatreonData
    {
        string Email { get; set; }
        string FullName { get; set; }
        string RewardTitle { get; set; }
        long PatreonId { get; set; }
        string PledgeAmountCents { get; set; }
        int Tier { get; set; }
        string Status { get; set; }
        string TwitchUserId { get; set; }
        string TwitchUrl { get; set; }
    }
}
