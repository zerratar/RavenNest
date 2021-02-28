using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Providers;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RavenNest.BusinessLogic.Game
{
    public class PlayerHighscoreProvider : IPlayerHighscoreProvider
    {
        private readonly ILogger<PlayerHighscoreProvider> logger;
        private readonly IPropertyProvider propertyProvider;

        public PlayerHighscoreProvider(
            ILogger<PlayerHighscoreProvider> logger,
            IPropertyProvider propertyProvider)
        {
            this.logger = logger;
            this.propertyProvider = propertyProvider;
        }

        public HighScoreItem GetSkillHighScore(Player player, IReadOnlyList<Player> players, string skill)
        {
            var allHighscorePlayers = GetSkillHighScore(players, skill, 0, int.MaxValue);
            return allHighscorePlayers.Players.FirstOrDefault(x => x.CharacterId == player.Id);
        }

        public HighScoreCollection GetSkillHighScore(IReadOnlyList<Player> players, string skill, int skip = 0, int take = 100)
        {
            try
            {
                if (skill == "All")
                    skill = null;

                //var players = playerManager.GetPlayers().Where(x => !x.IsAdmin && x.CharacterIndex == 0).ToList();
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
            catch (Exception exc)
            {
                logger.LogError("Unable to load highscore: " + exc.ToString());
                return new HighScoreCollection();
            }
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

        public HighScoreCollection GetHighScore(IReadOnlyList<Player> players, int skip = 0, int take = 100)
        {
            return GetSkillHighScore(players, null, skip, take);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private HighScoreItem Map(int rank, string skill, Player player)
        {
            TryGetSkillExperience(skill, player.Skills, out var exp, out var level);
            return new HighScoreItem
            {
                CharacterIndex = player.CharacterIndex,
                CharacterId = player.Id,
                PlayerName = player.Name,
                Level = level,
                Experience = Math.Floor(exp),
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
