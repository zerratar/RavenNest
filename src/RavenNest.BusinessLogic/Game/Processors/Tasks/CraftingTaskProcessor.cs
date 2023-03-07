using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using System;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class CraftingTaskProcessor : ResourceTaskProcessor
    {
        public override void Process(
            IIntegrityChecker integrityChecker,
            GameData gameData,
            PlayerInventoryProvider inventoryProvider,
            DataModels.GameSession session,
            Character character,
            CharacterState characterState)
        {
            var now = DateTime.UtcNow;
            var resources = gameData.GetResources(character.ResourcesId);
            if (resources == null)
            {
                resources = new DataModels.Resources
                {
                    Id = Guid.NewGuid(),
                };
                gameData.Add(resources);
                character.ResourcesId = resources.Id;
            }
            var state = gameData.GetCharacterSessionState(session.Id, character.Id);
            if (now - state.LastTaskUpdate >= TimeSpan.FromSeconds(ItemDropRateSettings.ResourceGatherInterval))
            {
                session.Updated = DateTime.UtcNow;
                state.LastTaskUpdate = DateTime.UtcNow;
                state.SailingRewardAttempted = DateTime.MinValue;

                if (resources.Ore >= OrePerIngot)
                {
                    resources.Ore -= OrePerIngot;
                    IncrementItemStack(gameData, inventoryProvider, session, character, IngotId);
                }

                if (resources.Wood >= WoodPerPlank)
                {
                    resources.Wood -= WoodPerPlank;
                    IncrementItemStack(gameData, inventoryProvider, session, character, PlankId);
                }

                UpdateResources(gameData, session, character, resources);
            }
        }
    }
}
