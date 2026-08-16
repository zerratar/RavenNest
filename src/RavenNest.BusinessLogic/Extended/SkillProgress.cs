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
            if (skill == null)
                return 0;

            return ExpBetweenLevels(skill.Level, skill.Experience, targetLevel);
        }

        /// <summary>
        ///     Experience owed to get from one level to another, without needing a character.
        /// </summary>
        /// <param name="experienceIntoLevel">
        ///     Progress already made within <paramref name="fromLevel"/>. Zero for a plain
        ///     level to level question, and a skill's own Experience when asking about a real one,
        ///     which counts within the current level rather than as a running total.
        /// </param>
        /// <remarks>
        ///     Split out of <see cref="ExpToLevel(PlayerSkill, int)"/> so the same curve answers
        ///     both "how far is my character" and "how much is a level worth", rather than the
        ///     calculator growing a second copy of the arithmetic that could drift from this one.
        /// </remarks>
        public static double ExpBetweenLevels(int fromLevel, double experienceIntoLevel, int targetLevel)
        {
            if (targetLevel <= fromLevel)
                return 0;

            var first = Math.Max(1, fromLevel);
            var last = Math.Min(targetLevel, MaxLevel);
            if (last <= first)
                return 0;

            var total = Math.Max(0, GameMath.ExperienceForLevel(first + 1) - experienceIntoLevel);
            for (var level = first + 2; level <= last; level++)
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
