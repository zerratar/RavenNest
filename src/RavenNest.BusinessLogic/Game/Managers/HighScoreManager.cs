using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public class HighScoreManager : IHighScoreManager
    {
        private readonly IPlayerManager playerManager;

        public HighScoreManager(IPlayerManager playerManager)
        {
            this.playerManager = playerManager;
        }

        public HighScoreCollection GetSkillHighScore(string skill, int skip = 0, int take = 100)
        {
            var players = playerManager.GetPlayers();
            var items = players
                .OrderByDescending(x => TryGetSkillExperience(skill, x.Skills, out var exp, out var level) ? exp : 0)
                .ThenByDescending(x => TryGetSkillExperience(skill, x.Skills, out var exp, out var level) ? level : 0)                
                .Skip(skip)
                .Take(take)
                .Select((x, y) => Map(y + 1, skill, x))
                .ToList();

            return new HighScoreCollection
            {
                Players = items,
                Skill = skill,
                Offset = 0,
                Total = players.Count
            };
        }

        public HighScoreCollection GetHighScore(int skip = 0, int take = 100)
        {
            var players = playerManager.GetPlayers();
            var items = players
                .OrderByDescending(x => TryGetSkillExperience(null, x.Skills, out var exp, out var level) ? level : 0)
                .ThenByDescending(x => TryGetSkillExperience(null, x.Skills, out var exp, out var level) ? exp : 0)
                .Skip(skip)
                .Take(take)
                .Select((x, y) => Map(y + 1, null, x))
                .ToList();

            return new HighScoreCollection
            {
                Players = items,
                Skill = null,
                Offset = skip,
                Total = players.Count
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static HighScoreItem Map(int rank, string skill, Player player)
        {
            TryGetSkillExperience(skill, player.Skills, out var exp, out var level);
            return new HighScoreItem
            {
                PlayerName = player.Name,
                Level = level,
                Experience = exp,
                Rank = rank,
                Skill = skill
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetSkillExperience(string skill, Skills skills, out decimal exp, out int level)
        {
            var props = skills.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.PropertyType == typeof(decimal));

            exp = 0;
            level = 0;

            if (string.IsNullOrEmpty(skill))
            {
                foreach (var property in props)
                {
                    var experience = (decimal)property.GetValue(skills);
                    level += GameMath.ExperienceToLevel(experience);
                    exp += experience;
                }

                exp = Math.Floor(exp);
                return true;
            }

            var targetProperty = props.FirstOrDefault(x =>
                x.PropertyType == typeof(decimal) && x.Name.Equals(skill, StringComparison.OrdinalIgnoreCase));

            if (targetProperty == null)
            {
                return false;
            }

            exp = Math.Floor((decimal)targetProperty.GetValue(skills));
            level = GameMath.ExperienceToLevel(exp);
            return true;
        }
    }
}