using RavenNest.BusinessLogic.Extended;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Blazor.Services
{
    /// <summary>
    ///     Matching what the game says a character is training against the skill rows the website
    ///     renders.
    ///
    ///     This lived inside PlayerSkills until the skills view was split into an overview tab and
    ///     a skills tab. Both need it, for different reasons: the overview to work out which skill
    ///     levels next, the skills list to mark the row that is being trained. Two copies of this
    ///     would drift, and it is the piece most likely to change, because the names the game sends
    ///     ("atk", "heal", "all") are not the names the site shows.
    /// </summary>
    public static class TrainingSkills
    {
        /// <summary>
        ///     Whether <paramref name="skill"/> is one of the skills currently being trained.
        ///     Training "all" advances attack, defense and strength together, so more than one row
        ///     can be true at once.
        /// </summary>
        public static bool IsTraining(PlayerSkill skill, TrainingSkill trainingSkill, bool isSailing)
        {
            if (skill?.Name == null)
                return false;

            var n = skill.Name.ToLower();

            if (isSailing && skill.Name.Equals("Sailing", StringComparison.OrdinalIgnoreCase))
                return true;

            if (trainingSkill?.Name == null)
                return false;

            var t = trainingSkill.Name.ToLower();

            if (n.StartsWith(t, StringComparison.OrdinalIgnoreCase))
                return true;

            if (t == "heal")
                return n.Equals("healing", StringComparison.OrdinalIgnoreCase);

            if (t == "all" || t == "health")
                return n.Equals("attack", StringComparison.OrdinalIgnoreCase)
                    || n.Equals("defense", StringComparison.OrdinalIgnoreCase)
                    || n.Equals("strength", StringComparison.OrdinalIgnoreCase);

            if (n == "attack" && t == "atk")
                return true;

            if (t == "mine" && n.Equals("mining", StringComparison.OrdinalIgnoreCase))
                return true;

            if (t == "gather" && n.Equals("gathering", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        /// <summary>
        ///     Which skill levels next. When several train at once, which is what !train all does,
        ///     the useful answer is whichever is closest rather than whichever comes first in the
        ///     list.
        /// </summary>
        public static PlayerSkill NextUp(IReadOnlyList<PlayerSkill> skills, TrainingSkill trainingSkill, bool isSailing)
        {
            return skills?
                .Where(x => IsTraining(x, trainingSkill, isSailing))
                .OrderBy(SkillProgress.ExpToNextLevel)
                .FirstOrDefault();
        }

        /// <summary>
        ///     The name to show for what is being trained, translated out of the game's vocabulary.
        /// </summary>
        public static string DisplayName(IReadOnlyList<PlayerSkill> skills, TrainingSkill trainingSkill, bool isSailing)
        {
            if (trainingSkill?.Name == null)
                return null;

            if (trainingSkill.Name.Equals("health", StringComparison.OrdinalIgnoreCase) ||
                trainingSkill.Name.Equals("all", StringComparison.OrdinalIgnoreCase))
                return "All";

            if (trainingSkill.Name.Equals("heal", StringComparison.OrdinalIgnoreCase))
                return "Healing";

            if (skills == null)
                return null;

            return skills.FirstOrDefault(x => IsTraining(x, trainingSkill, isSailing))?.Name;
        }
    }
}
