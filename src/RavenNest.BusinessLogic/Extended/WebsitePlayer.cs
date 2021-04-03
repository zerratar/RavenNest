using RavenNest.Models;

namespace RavenNest.BusinessLogic.Extended
{
    public class WebsitePlayer : Player
    {
        public new SkillsExtended Skills { get; set; }

        public CharacterSessionInfo SessionInfo { get; set; }

        public int CombatLevel
        {
            get
            {
                if (this.Skills == null)
                    return 3;
                return (int)(((Skills.AttackLevel + Skills.DefenseLevel + Skills.HealthLevel + Skills.StrengthLevel) / 4f) + ((Skills.RangedLevel + Skills.MagicLevel + Skills.HealingLevel) / 8f));
            }
        }
    }
}
