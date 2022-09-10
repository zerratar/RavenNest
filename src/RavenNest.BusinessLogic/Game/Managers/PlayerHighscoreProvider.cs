using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Skills = RavenNest.Models.Skills;

namespace RavenNest.BusinessLogic.Game
{
    public class PlayerHighscoreProvider : IPlayerHighscoreProvider
    {
        private readonly IGameData gameData;
        private readonly ILogger<PlayerHighscoreProvider> logger;
        private readonly IPropertyProvider propertyProvider;

        public PlayerHighscoreProvider(
            IGameData gameData,
            ILogger<PlayerHighscoreProvider> logger,
            IPropertyProvider propertyProvider)
        {
            this.gameData = gameData;
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
            if(skill== "All" || skill == null)
            {
                var playerSelection = players
                    .OrderByDescending(x => OrderByLevel(null, x))
                   .ThenByDescending(x => OrderByExp(null, x))
                .Skip(skip)
                .Take(take).ToList();

                var items = playerSelection.Select((x, y) => Map(y + 1, null, x, new List<CharacterSkillRecord>())).ToList();

                return new HighScoreCollection
                {
                    Players = items,
                    Skill = skill,
                    Offset = skip,
                    Total = players.Count
                };
            }
            else
            {

                var charactersSkillRecords = gameData.GetSkillRecords(DataModels.Skills.SkillNames.IndexOf(skill))
                    .OrderByDescending(level => level.SkillLevel)
                    .ThenBy(reached => reached.DateReached)
                    .Skip(skip)
                    .Take(take).ToList();
                var rank = 1;
                var items = new List<HighScoreItem>();

                foreach(var skillRecord in charactersSkillRecords)
                {
                    var player = players.FirstOrDefault(e => e.Id == skillRecord.CharacterId);
                    if (player != null)
                    {
                        var item = Map(rank, skill, player, charactersSkillRecords);
                        if(skillRecord.SkillLevel != GameMath.MaxLevel)
                            item.DateReached = skillRecord.DateReached; //keeping the dateReached when it's not max level
                        items.Add(item);

                        rank++;
                    }
                }

                return new HighScoreCollection
                {
                    Players = items,
                    Skill = skill,
                    Offset = skip,
                    Total = players.Count
                };
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int OrderByLevel(string skill, Player x)
        {
            return TryGetLevel(skill, x.Skills, out var level) ? level : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double OrderByExp(string skill, Player x)
        {
            return TryGetExperience(skill, x.Skills, out var exp) ? exp : 0;
        }

        public HighScoreCollection GetHighScore(IReadOnlyList<Player> players, int skip = 0, int take = 100)
        {
            return GetSkillHighScore(players, null, skip, take);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private HighScoreItem Map(int rank, string skill, Player player, IReadOnlyList<CharacterSkillRecord> skillRecords)
        {
            TryGetSkillExperience(
                skill,
                player.Id,
                player.Skills,
                skillRecords,
                out var exp,
                out var level,
                out var dateReached,
                out var order);


            return new HighScoreItem
            {
                CharacterIndex = player.CharacterIndex,
                CharacterId = player.Id,
                PlayerName = player.Name,
                Level = level,
                DateReached = dateReached,
                OrderAchieved = order == 0 ? rank : order,
                Experience = Math.Floor(exp),
                Rank = rank,
                Skill = skill
            };
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetLevel(string skill, Skills skills, out int level)
        {
            var lvlProps = propertyProvider.GetProperties<Skills, int>();
            level = 0;
            if (string.IsNullOrEmpty(skill))
            {
                foreach (var prop in lvlProps)
                {
                    level += (int)prop.GetValue(skills);
                }
                return true;
            }

            var lvlProp = lvlProps.FirstOrDefault(x => x.Name.IndexOf(skill, StringComparison.OrdinalIgnoreCase) >= 0);
            if (lvlProp == null)
                return false;

            level = (int)lvlProp.GetValue(skills);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetExperience(string skill, Skills skills, out double exp)
        {
            var expProps = propertyProvider.GetProperties<Skills, double>();
            exp = 0;

            if (string.IsNullOrEmpty(skill))
            {
                foreach (var prop in expProps)
                {
                    exp += (double)prop.GetValue(skills);
                }
                return true;
            }

            var expProp = expProps.FirstOrDefault(x => x.Name.Equals(skill, StringComparison.OrdinalIgnoreCase));
            if (expProp == null)
                return false;

            exp = Math.Floor((double)expProp.GetValue(skills));
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetSkillExperience(string skill, Guid characterId, Skills skills, IReadOnlyList<CharacterSkillRecord> skillRecords, out double exp, out int level, out DateTime dateReached, out int order)
        {
            dateReached = DateTime.UnixEpoch; //Make it more obvious it's an incorrect date            
            order = -1;

            var ok = TryGetExperience(skill, skills, out exp) & TryGetLevel(skill, skills, out level);

            if (level == GameMath.MaxLevel)
            {
                var skillIndex = DataModels.Skills.SkillNames.IndexOf(skill);
                var record = gameData.GetCharacterSkillRecord(characterId, skillIndex);
                if (record != null)
                {
                    dateReached = record.DateReached;
                    order = 1;
                    if (skillRecords.Count > 0)
                    {
                        foreach (var r in skillRecords)
                        {
                            if (r.Id == record.Id)
                            {
                                break;
                            }
                            ++order;
                        }
                    }
                    else
                    {
                        order = 0;
                    }
                }
            }
            return ok;
        }
    }
}
