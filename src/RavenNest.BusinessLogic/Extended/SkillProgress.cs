using System;

namespace RavenNest.BusinessLogic.Extended
{
    /// <summary>
    ///     Level and time to level maths for the website.
    ///
    ///     The site has only ever shown a bare percentage, which tells a player how far along a
    ///     skill is but not how much is left or how long it will take. Everything needed to answer
    ///     that is already stored, it was simply never joined up: the experience curve is in
    ///     <see cref="GameMath"/> and the rate arrives with the character state as ExpPerHour.
    ///
    ///     Note that <see cref="PlayerSkill.Experience"/> counts progress within the current level
    ///     only, not a running total, which is why crossing several levels means adding the levels
    ///     in between whole.
    /// </summary>
    public static class SkillProgress
    {
        public const int MaxLevel = GameMath.MaxLevel;

        public static bool IsMaxLevel(PlayerSkill skill)
        {
            return skill == null || skill.Level >= MaxLevel;
        }

        /// <summary>
        ///     Experience still owed before this skill reaches its next level.
        /// </summary>
        public static double ExpToNextLevel(PlayerSkill skill)
        {
            if (IsMaxLevel(skill))
                return 0;

            return Math.Max(0, GameMath.ExperienceForLevel(skill.Level + 1) - skill.Experience);
        }

        /// <summary>
        ///     Experience still owed before this skill reaches <paramref name="targetLevel"/>.
        /// </summary>
        public static double ExpToLevel(PlayerSkill skill, int targetLevel)
        {
            if (skill == null || targetLevel <= skill.Level)
                return 0;

            var total = ExpToNextLevel(skill);
            var last = Math.Min(targetLevel, MaxLevel);
            for (var level = skill.Level + 2; level <= last; level++)
            {
                total += GameMath.ExperienceForLevel(level);
            }

            return total;
        }

        /// <summary>
        ///     How long <paramref name="experience"/> takes at <paramref name="expPerHour"/>.
        ///
        ///     Null when there is no rate to divide by, which is the case for every skill the
        ///     character is not currently training. Showing nothing is the honest answer there; a
        ///     made up estimate would be worse than none.
        /// </summary>
        public static TimeSpan? TimeFor(double experience, long? expPerHour)
        {
            if (expPerHour == null || expPerHour <= 0 || experience <= 0)
                return null;

            var hours = experience / expPerHour.Value;
            if (double.IsNaN(hours) || double.IsInfinity(hours) || hours > TimeSpan.MaxValue.TotalHours)
                return null;

            return TimeSpan.FromHours(hours);
        }
    }
}
