using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class MiningTaskProcessor : ResourceTaskProcessor
    {
        private readonly Random random = new Random();

        public override void Handle(
            IIntegrityChecker integrityChecker,
            IGameData gameData,
            IPlayerInventoryProvider inventoryProvider,
            DataModels.GameSession session,
            Character character,
            CharacterState state)
        {
            UpdateResourceGain(integrityChecker, gameData, inventoryProvider, session, character, resources =>
            {
                session.Updated = DateTime.UtcNow;
                var skills = gameData.GetSkills(character.SkillsId);
                var miningLevel = GameMath.ExperienceToLevel(skills.Mining);
                var chance = Random.NextDouble();

                var multiDrop = Random.NextDouble();
                var isMultiDrop = multiDrop >= 0.5;

                // clamp to always be 10% chance on each resource gain.
                // so we dont get drops too often.
                if (chance <= 0.1)
                {
                    foreach (var res in DroppableResources.OrderBy(x => random.NextDouble()))
                    {
                        if (miningLevel >= res.SkillLevel && chance <= res.GetDropChance(miningLevel))
                        {
                            IncrementItemStack(gameData, inventoryProvider, session, character, res.Id);
                            if (isMultiDrop)
                            {
                                isMultiDrop = false;
                                continue;
                            }
                            break;
                        }
                    }
                }

                ++resources.Ore;
            });
        }
    }
}
