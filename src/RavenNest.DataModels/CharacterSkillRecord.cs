using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class CharacterSkillRecord : Entity<CharacterSkillRecord>
    {
        [PersistentData] private int skillIndex;
        [PersistentData] private string skillName;
        [PersistentData] private int skillLevel;
        [PersistentData] private double skillExperience;
        [PersistentData] private Guid characterId;
        [PersistentData] private DateTime dateReached;
    }
}
