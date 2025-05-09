namespace RavenNest.Models
{
    public class SessionSettings
    {
        public bool IsAdministrator { get; set; }
        public bool IsModerator { get; set; }
        public int SubscriberTier { get; set; }
        public int ExpMultiplierLimit { get; set; }
        public int PlayerExpMultiplierLimit { get; set; }
        public bool StrictLevelRequirements { get; set; }
        public double DungeonExpFactor { get; set; }
        public double RaidExpFactor { get; set; }

        public int AutoRestCost { get; set; }
        public int AutoJoinDungeonCost { get; set; }
        public int AutoJoinRaidCost { get; set; }

        public double XP_EasyLevel { get; set; }
        public double XP_IncrementMins { get; set; }
        public double XP_EasyLevelIncrementDivider { get; set; }
        public double XP_GlobalMultiplierFactor { get; set; }
    }
}
