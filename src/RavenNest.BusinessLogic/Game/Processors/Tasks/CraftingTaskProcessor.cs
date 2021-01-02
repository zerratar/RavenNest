using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using System;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class CraftingTaskProcessor : ResourceTaskProcessor
    {
        public override void Handle(
            IIntegrityChecker integrityChecker,
            IGameData gameData,
            IPlayerInventoryProvider inventoryProvider,
            DataModels.GameSession session,
            Character character,
            CharacterState characterState)
        {
            var now = DateTime.UtcNow;
            var resources = gameData.GetResources(character.ResourcesId);
            var state = gameData.GetCharacterSessionState(session.Id, character.Id);
            if (now - state.LastTaskUpdate >= TimeSpan.FromSeconds(ResourceGatherInterval))
            {
                session.Updated = DateTime.UtcNow;
                state.LastTaskUpdate = DateTime.UtcNow;

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
