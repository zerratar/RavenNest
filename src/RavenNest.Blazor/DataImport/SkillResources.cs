using System;

namespace RavenNest
{
    [Serializable]
    public class SkillResources
    {
        public SkillResource Mining = new SkillResource
        {
            Value = 0
        };

        public SkillResource Woodcutting = new SkillResource
        {
            Value = 0
        };

        public SkillResource Fishing = new SkillResource
        {
            Value = 0
        };

        public SkillResource Farming = new SkillResource
        {
            Value = 0
        };

        public SkillResource Coins = new SkillResource
        {
            Value = 0
        };
    }
}
