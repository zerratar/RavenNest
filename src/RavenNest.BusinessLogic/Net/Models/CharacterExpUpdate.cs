using System;

namespace RavenNest.BusinessLogic.Net
{
    public class CharacterExpUpdate
    {
        public Guid CharacterId { get; set; }
        public int SkillIndex { get; set; }
        public int Level { get; set; }
        public double Experience { get; set; }
        public float Percent { get; set; }
    }
}
