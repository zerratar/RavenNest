using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models.Tv;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.BusinessLogic.Tv
{
    public class RavenfallTvEpisodePromptGenerator
    {
        private readonly GameData gameData;

        public RavenfallTvEpisodePromptGenerator(GameData gameData)
        {
            this.gameData = gameData;
        }

        public string Generate(GenerateUserEpisodeRequest req)
        {
            var showName = gameData.GetUserProperty(req.UserId, UserProperties.RavenfallTvShowName);
            var showDescription = gameData.GetUserProperty(req.UserId, UserProperties.RavenfallTvShowDescription);
            var ep = req.Request;
            return
                new RavenfallTvEpisodePromptBuilder()
                .SetShowTitle(showName)
                .SetShowDescription(showDescription)
                .SetEpisodeTitle(ep.Title)
                .SetEpisodeDescription(ep.Description)
                .SetCharacters(GetCharacters(req.Request.Characters))
                .ToString();
        }

        private List<Episode.Character> GetCharacters(List<Episode.Character> characters)
        {
            foreach (var c in characters)
            {
                Character target = null;
                if (Guid.TryParse(c.Id, out var cid))
                {
                    target = gameData.GetCharacter(cid);
                }

                if (target == null)
                {
                    target = gameData.GetCharacterByName(c.Name);
                }

                if (target != null)
                {
                    var appearance = gameData.GetAppearance(target.SyntyAppearanceId);

                    // Don't override name. only set it if its empty, since we could be using an Alias/Nickname
                    // for the character but have the correct Character Id
                    if (string.IsNullOrEmpty(c.Name))
                        c.Name = c.Name;

                    c.Gender = appearance.Gender == Gender.Male ? "male" : "female";
                    c.Strength = DetermineSkillStrength(gameData.GetCharacterSkills(target.SkillsId));
                    if (string.IsNullOrEmpty(c.Description) && !string.IsNullOrEmpty(target.Description))
                    {
                        c.Description = target.Description;
                    }
                }
            }
            return characters;
        }

        private int DetermineSkillStrength(Skills skills)
        {
            // we will use the strongest combat skill to determine the skill level.
            return Max(skills.AttackLevel, skills.DefenseLevel, skills.StrengthLevel, skills.HealthLevel, skills.RangedLevel, skills.MagicLevel, skills.HealingLevel);
        }

        private static int Max(params int[] values)
        {
            return values.Max();
        }
    }
}
