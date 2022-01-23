using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using System;
using System.Linq;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class MiningTaskProcessor : ResourceTaskProcessor
    {
        public override void Process(
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
                var skills = gameData.GetCharacterSkills(character.SkillsId);
                if (skills == null)
                    return;

                var miningLevel = skills.MiningLevel;
                var multiDrop = Random.NextDouble();
                var isMultiDrop = multiDrop <= 0.1;
                var chance = Random.NextDouble();
                if (chance <= ItemDropRateSettings.InitDropChance)
                {
                    foreach (var res in DroppableResources.OrderByDescending(x => x.SkillLevel))
                    {
                        chance = Random.NextDouble();
                        if (miningLevel >= res.SkillLevel && (chance <= res.GetDropChance(miningLevel)))
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

                var villageResources = GetVillageResources(gameData, session);
                if (villageResources != null)
                {
                    ++villageResources.Ore;
                }
            });
        }
    }
}
