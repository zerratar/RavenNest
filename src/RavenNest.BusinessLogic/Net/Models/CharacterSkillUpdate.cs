using System;

namespace RavenNest.BusinessLogic.Net
{
    public class CharacterSkillUpdate
    {
        public Guid CharacterId { get; set; }
        public string UserId { get; set; }
        public double[] Experience { get; set; }
        public int[] Level { get; set; }
    }

}
