using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Models;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

        public HighScoreItem GetSkillHighScore(Player player, IReadOnlyDictionary<Guid, HighscorePlayer> players, string skill)
        {
            var allHighscorePlayers = GetSkillHighScore(players, skill, 0, int.MaxValue);
            return allHighscorePlayers.Players.FirstOrDefault(x => x.CharacterId == player.Id);
        }

        public HighScoreCollection GetSkillHighScore(IReadOnlyDictionary<Guid, HighscorePlayer> playerLookup, string skill, int skip = 0, int take = 100)
        {
            if (skill == "All" || skill == null)
            {
                return GetHighScoreAllSkills(playerLookup, skip, take);
            }
            else
            {
                var skillIndex = DataModels.Skills.SkillNames.IndexOf(skill);
                var skillRecords = gameData.GetSkillRecords(skillIndex, (ICollection<Guid>)playerLookup.Keys).ToSpan();

                var size = Math.Min(take, skillRecords.Length - skip);
                var items = new HighScoreItem[size];

                var sortedRecords = SortRecords(skillRecords, skip, take);
                for (int i = 0; i < sortedRecords.Length; i++)
                {
                    var skillRecord = sortedRecords[i];
                    var player = playerLookup[skillRecord.CharacterId];
                    var experience = player.Skills.GetExperience(skillIndex);
                    var rank = i + 1;

                    items[i] = new HighScoreItem
                    {
                        CharacterId = player.Id,
                        PlayerName = player.Name,
                        CharacterIndex = player.CharacterIndex,
                        Skill = skill,
                        Experience = experience,
                        Level = skillRecord.SkillLevel,
                        DateReached = skillRecord.DateReached,
                        OrderAchieved = rank,
                        Rank = rank,
                    };
                }

                return new HighScoreCollection
                {
                    Players = items,
                    Skill = skill,
                    Offset = skip,
                    Total = playerLookup.Count
                };
            }
        }

        public HighScoreCollection GetHighScoreAllSkills(IReadOnlyDictionary<Guid, HighscorePlayer> players, int skip, int take)
        {
            var playerSelection = players.Values
                .OrderByDescending(x => OrderByLevel(-1, x))
                .ThenByDescending(x => OrderByExp(-1, x))
                .Skip(skip)
                .Take(take).ToList();

            var items = playerSelection.Select((x, y) => Map(y + 1, -1, x, new List<CharacterSkillRecord>())).ToList();

            return new HighScoreCollection
            {
                Players = items,
                Skill = "All",
                Offset = skip,
                Total = players.Count
            };
        }

        private Span<CharacterSkillRecord> SortRecords(Span<CharacterSkillRecord> input, int skip, int take)
        {
            int Sort(CharacterSkillRecord a, CharacterSkillRecord b)
            {
                if (a.SkillLevel < b.SkillLevel)
                {
                    return -1;
                }

                else if (a.SkillLevel == b.SkillLevel)
                {

                    if (a.DateReached < b.DateReached)
                    {
                        return -1;
                    }

                    // no need to compare with dates being same time. This will most likely never happen.
                    // and since we return 1 per default already, code-wise this is not necessary
                    // return 1;
                }
                return 1;
            }

            input.Sort(new Comparison<CharacterSkillRecord>(Sort));
            var size = Math.Min(take, input.Length - skip);
            return input.Slice(skip, size);

            //    var size = Math.Min(take, input.Count - skip); // size is not guaranteed to be correct, but will be a maximum possible
            //    var items = new List<CharacterSkillRecord>(size);
            //    var takeIndex = 0;
            //    var skipIndex = 0;

            //    foreach (var item in input
            //        .OrderByDescending(level => level.SkillLevel)
            //        .ThenBy(reached => reached.DateReached))
            //    {
            //        if (!filter.ContainsKey(item.CharacterId))
            //            continue;

            //        if (skipIndex >= skip)
            //        {
            //            items.Add(item);
            //            takeIndex++;
            //        }

            //        skipIndex++;

            //        if (takeIndex >= take)
            //        {
            //            break;
            //        }
            //    }

            //    return items;
        }




        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int OrderByLevel(int skillIndex, HighscorePlayer x)
        {
            return TryGetLevel(skillIndex, x.Skills, out var level) ? level : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double OrderByExp(int skillIndex, HighscorePlayer x)
        {
            return TryGetExperience(skillIndex, x.Skills, out var exp) ? exp : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private HighScoreItem Map(int rank, int skillIndex, HighscorePlayer player, IReadOnlyList<CharacterSkillRecord> skillRecords)
        {
            TryGetSkillExperience(
                skillIndex,
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
                Skill = skillIndex >= 0 ? DataModels.Skills.SkillNames[skillIndex] : "all"
            };
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetLevel(int skillIndex, DataModels.Skills skills, out int level)
        {
            level = 0;
            if (skillIndex < 0)
            {
                foreach (var skill in skills.GetSkills())
                {
                    level += skill.Level;
                }
                return true;
            }

            level = skills.GetLevel(skillIndex);
            return true;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetExperience(int skillIndex, DataModels.Skills skills, out double exp)
        {
            exp = 0;

            if (skillIndex < 0)
            {
                foreach (var skill in skills.GetSkills())
                {
                    exp += skill.Experience;
                }
                return true;
            }

            exp = skills.GetExperience(skillIndex);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetSkillExperience(int skillIndex, Guid characterId, DataModels.Skills skills, IReadOnlyList<CharacterSkillRecord> skillRecords, out double exp, out int level, out DateTime dateReached, out int order)
        {
            dateReached = DateTime.UnixEpoch; //Make it more obvious it's an incorrect date            
            order = -1;

            var ok = TryGetExperience(skillIndex, skills, out exp) & TryGetLevel(skillIndex, skills, out level);

            if (level == GameMath.MaxLevel)
            {
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
