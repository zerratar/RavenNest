using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using RavenNest.BusinessLogic.Providers;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public class HighScoreManager : IHighScoreManager
    {
        private readonly IPropertyProvider propertyProvider;
        private readonly IPlayerManager playerManager;

        public HighScoreManager(IPropertyProvider propertyProvider, IPlayerManager playerManager)
        {
            this.propertyProvider = propertyProvider;
            this.playerManager = playerManager;
        }

        public HighScoreCollection GetSkillHighScore(string skill, int skip = 0, int take = 100)
        {
            var players = playerManager.GetPlayers().Where(x => !x.IsAdmin && x.CharacterIndex == 0).ToList();

            var items =
                (skill == null
                ? (players
                    .OrderByDescending(x => OrderByLevel(skill, x))
                    .ThenByDescending(x => OrderByExp(skill, x)))
                : (players
                    .OrderByDescending(x => OrderByExp(skill, x))
                    .ThenByDescending(x => OrderByLevel(skill, x))))
                .Skip(skip)
                .Take(take)
                .Select((x, y) => Map(y + 1, skill, x))
                .ToList();

            return new HighScoreCollection
            {
                Players = items,
                Skill = skill,
                Offset = skip,
                Total = players.Count
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int OrderByLevel(string skill, Player x)
        {
            return TryGetSkillExperience(skill, x.Skills, out _, out var level) ? level : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private decimal OrderByExp(string skill, Player x)
        {
            return TryGetSkillExperience(skill, x.Skills, out var exp, out _) ? exp : 0;
        }

        public HighScoreCollection GetHighScore(int skip = 0, int take = 100)
        {
            return GetSkillHighScore(null, skip, take);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private HighScoreItem Map(int rank, string skill, Player player)
        {
            TryGetSkillExperience(skill, player.Skills, out var exp, out var level);
            return new HighScoreItem
            {
                CharacterIndex = player.CharacterIndex,
                PlayerName = player.Name,
                Level = level,
                Experience = exp,
                Rank = rank,
                Skill = skill
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetSkillExperience(string skill, Skills skills, out decimal exp, out int level)
        {
            var props = propertyProvider.GetProperties<Skills, decimal>();

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
