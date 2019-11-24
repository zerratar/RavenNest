using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;
using System;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public abstract class ResourceTaskProcessor : PlayerTaskProcessor
    {
        protected const int ResourceGatherInterval = 6;
        protected const double RuneNuggetDropChance = 0.01;
        protected const double AdamantiteNuggetDropChance = 0.05;
        protected const double MithrilNuggetDropChance = 0.10;
        protected const double SteelNuggetDropChance = 0.20;
        protected const double IronNuggetDropChance = 0.25;
        protected const double DropChanceIncrement = 0.02;

        protected const int OrePerIngot = 10;
        protected const int WoodPerPlank = 10;

        protected void UpdateResourceGain(IGameData gameData, GameSession session, Character character, Action<DataModels.Resources> onUpdate)
        {
            var now = DateTime.UtcNow;
            var state = gameData.GetCharacterSessionState(session.Id, character.Id);
            if (now - state.LastTaskUpdate >= TimeSpan.FromSeconds(ResourceGatherInterval))
            {
                var resources = gameData.GetResources(character.ResourcesId);
                var oldWood = resources.Wood;
                var oldWheat = resources.Wheat;
                var oldFish = resources.Fish;
                var oldOre = resources.Ore;
                var oldCoins = resources.Coins;

                state.LastTaskUpdate = DateTime.UtcNow;
                onUpdate?.Invoke(resources);

                if (oldCoins != resources.Coins ||
                    oldWood != resources.Wood ||
                    oldWheat != resources.Wheat ||
                    oldFish != resources.Fish ||
                    oldOre != resources.Ore)
                    UpdateResources(gameData, session, character, resources);
            }
        }

        protected void UpdateResources(IGameData gameData, GameSession session, Character character, DataModels.Resources resources)
        {
            var user = gameData.GetUser(character.UserId);
            var gameEvent = gameData.CreateSessionEvent(GameEventType.ResourceUpdate, session,
                new ResourceUpdate
                {
                    UserId = user.UserId,
                    FishAmount = resources.Fish,
                    OreAmount = resources.Ore,
                    WheatAmount = resources.Wheat,
                    WoodAmount = resources.Wood,
                    CoinsAmount = resources.Coins
                });

            gameData.Add(gameEvent);
        }
    }
}
