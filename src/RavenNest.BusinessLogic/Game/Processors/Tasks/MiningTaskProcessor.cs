using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class MiningTaskProcessor : ResourceTaskProcessor
    {
        public override void Handle(IGameData gameData, GameSession session, Character character, DataModels.CharacterState state)
        {
            UpdateResourceGain(gameData, session, character, resources =>
            {
                var skills = gameData.GetSkills(character.SkillsId);
                var miningLevel = GameMath.ExperienceToLevel(skills.Mining);
                var chance = Random.NextDouble();

                // clamp to always be 50% chance on each resource gain.
                // so we dont get drops too often.
                if (chance <= 0.5)
                {
                    foreach (var res in DroppableResources)
                    {
                        if (miningLevel >= res.SkillLevel && chance <= res.GetDropChance(miningLevel))
                        {
                            IncrementItemStack(gameData, session, character, res.Id);
                            break;
                        }
                    }
                }

                ++resources.Ore;
            });
        }
    }
}
