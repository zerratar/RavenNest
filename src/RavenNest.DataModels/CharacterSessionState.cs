using System;

namespace RavenNest.DataModels
{
    public class CharacterSessionState
    {
        public Guid SessionId { get; set; }
        public Guid CharacterId { get; set; }
        public DateTime LastTaskUpdate { get; set; }
        public ExpSkillGainCollection ExpGain { get; set; } = new ExpSkillGainCollection();
    }

    public class ExpSkillGainCollection
    {
        public ExpGain Attack { get; set; } = new ExpGain();
        public ExpGain Defense { get; set; } = new ExpGain();
        public ExpGain Strength { get; set; } = new ExpGain();
        public ExpGain Health { get; set; } = new ExpGain();
        public ExpGain Woodcutting { get; set; } = new ExpGain();
        public ExpGain Fishing { get; set; } = new ExpGain();
        public ExpGain Mining { get; set; } = new ExpGain();
        public ExpGain Crafting { get; set; } = new ExpGain();
        public ExpGain Cooking { get; set; } = new ExpGain();
        public ExpGain Farming { get; set; } = new ExpGain();
        public ExpGain Slayer { get; set; } = new ExpGain();
        public ExpGain Magic { get; set; } = new ExpGain();
        public ExpGain Ranged { get; set; } = new ExpGain();
        public ExpGain Sailing { get; set; } = new ExpGain();
    }

    public class ExpGain
    {
        public decimal Amount { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime LastUpdate { get; set; }

        public decimal ExpPerHour
        {
            get
            {
                var elapsed = DateTime.UtcNow - StartTime;
                return Amount / (decimal)elapsed.TotalHours;
            }
        }

        public void AddExperience(decimal amount)
        {
            if (StartTime == DateTime.MinValue)
                StartTime = DateTime.UtcNow;
            Amount += amount;
            LastUpdate = DateTime.UtcNow;
        }
    }
}