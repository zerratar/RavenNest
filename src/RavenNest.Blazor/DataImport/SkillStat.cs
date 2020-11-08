using System;
using RavenNest.BusinessLogic;

namespace RavenNest
{
    [Serializable]
    public class SkillStat
    {
        public string Name;
        public int CurrentValue;
        public int Level;
        public decimal Experience;

        public bool AddExp(decimal exp, out int newLevels)
        {
            newLevels = 0;
            this.Experience += exp;
            var newLevel = GameMath.OLD_ExperienceToLevel(Experience);
            var levelDiff = newLevel - Level;
            if (levelDiff > 0)
            {
                // celebrate!
                this.CurrentValue = newLevel;
                this.Level = newLevel;
                newLevels = levelDiff;
            }

            //Debug.Log(Name + ":: Add exp: " + exp + ", cur exp: " + this.Experience + ", cur lvl: " + Level + " level from exp: " + newLevel);

            return newLevels > 0;
        }

        public void Reset()
        {
            this.CurrentValue = this.Level;
        }

        public void Add(int value)
        {
            this.CurrentValue += value;
        }
    }
}
