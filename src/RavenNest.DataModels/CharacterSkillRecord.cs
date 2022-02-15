using System;

namespace RavenNest.DataModels
{
    public partial class CharacterSkillRecord : Entity<CharacterSkillRecord>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private int skillIndex; public int SkillIndex { get => skillIndex; set => Set(ref skillIndex, value); }
        private string skillName; public string SkillName { get => skillName; set => Set(ref skillName, value); }
        private int skillLevel; public int SkillLevel { get => skillLevel; set => Set(ref skillLevel, value); }
        private double skillExperience; public double SkillExperience { get => skillExperience; set => Set(ref skillExperience, value); }
        private Guid characterId; public Guid CharacterId { get => characterId; set => Set(ref characterId, value); }
        private DateTime dateReached; public DateTime DateReached { get => dateReached; set => Set(ref dateReached, value); }
    }
}
