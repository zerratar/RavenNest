using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class CraftingTaskProcessor : ResourceTaskProcessor
    {
        public override void Process(
            GameData gameData,
            PlayerInventoryProvider inventoryProvider,
            GameSession session,
            Character character,
            CharacterState characterState)
        {
            var now = DateTime.UtcNow;
            var resources = gameData.GetResources(character);
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
