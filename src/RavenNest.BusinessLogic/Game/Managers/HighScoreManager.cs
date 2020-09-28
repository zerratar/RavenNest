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
                players
                    .OrderByDescending(x => OrderByLevel(skill, x))
                    .ThenByDescending(x => OrderByExp(skill, x))
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
            var expProps = propertyProvider.GetProperties<Skills, decimal>();
            var lvlProps = propertyProvider.GetProperties<Skills, int>();

            exp = 0;
            level = 0;

            if (string.IsNullOrEmpty(skill))
            {
                foreach (var prop in expProps)
                {
                    exp += (decimal)prop.GetValue(skills);
                }
                foreach (var prop in lvlProps)
                {
                    level += (int)prop.GetValue(skills);
                }

                return true;
            }

            var expProp = expProps.FirstOrDefault(x => x.Name.Equals(skill, StringComparison.OrdinalIgnoreCase));
            if (expProp == null)
                return false;

            var lvlProp = lvlProps.FirstOrDefault(x => x.Name.IndexOf(skill, StringComparison.OrdinalIgnoreCase) >= 0);
            if (lvlProp == null)
                return false;

            exp = Math.Floor((decimal)expProp.GetValue(skills));
            level = (int)lvlProp.GetValue(skills);
            return true;
        }
    }
}
