using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public abstract class PlayerTaskProcessor : ITaskProcessor
    {
        protected readonly Random Random = new Random();

        protected void IncrementItemStack(
            IGameData gameData,
            IPlayerInventoryProvider inventoryProvider,
            DataModels.GameSession session,
            Character character, Guid itemId)
        {
            var inventory = inventoryProvider.Get(character.Id);
            inventory.AddItem(itemId);

            gameData.Add(gameData.CreateSessionEvent(GameEventType.ItemAdd, session, new ItemAdd
            {
                UserId = gameData.GetUser(character.UserId).UserId,
                Amount = 1,
                ItemId = itemId
            }));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static DataModels.InventoryItem CreateInventoryItem(Character character, Guid itemId)
        {
            return new DataModels.InventoryItem { Id = Guid.NewGuid(), Amount = 1, CharacterId = character.Id, Equipped = false, ItemId = itemId };
        }

        public abstract void Handle(
            IIntegrityChecker integrityChecker,
            IGameData gameData,
            IPlayerInventoryProvider inventoryProvider,
            DataModels.GameSession session,
            Character character,
            DataModels.CharacterState state);
    }
}
