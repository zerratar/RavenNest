using System;

namespace RavenNest.BusinessLogic.Net
{
    public class CharacterSkillUpdate
    {
        public Guid CharacterId { get; set; }
        public string UserId { get; set; }
        public decimal[] Experience { get; set; }
        public int[] Level { get; set; }
    }

}
