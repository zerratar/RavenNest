using System;

namespace RavenNest
{
    [Serializable]
    public class SkillStats
    {
        public SkillStat Woodcutting = new SkillStat
        {
            Name = "Woodcutting",
            CurrentValue = 1,
            Level = 1
        };

        public SkillStat Fishing = new SkillStat
        {
            Name = "Fishing",
            CurrentValue = 1,
            Level = 1
        };

        public SkillStat Farming = new SkillStat
        {
            Name = "Farming",
            CurrentValue = 1,
            Level = 1
        };

        public SkillStat Sailing = new SkillStat
        {

            Name = "Sailing",
            CurrentValue = 1,
            Level = 1
        };

        public SkillStat Crafting = new SkillStat
        {
            Name = "Crafting",
            CurrentValue = 1,
            Level = 1
        };

        public SkillStat Cooking = new SkillStat
        {
            Name = "Cooking",
            CurrentValue = 1,
            Level = 1
        };

        public SkillStat Mining = new SkillStat
        {
            Name = "Mining",
            CurrentValue = 1,
            Level = 1
        };

        public decimal ExpOverTime = 1;
    }
}
