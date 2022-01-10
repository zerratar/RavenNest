using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.BusinessLogic.Twitch.Extension;
using RavenNest.DataModels;
using RavenNest.Models;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public abstract class PlayerTaskProcessor : ITaskProcessor
    {
        protected readonly Random Random = new Random();
        public IExtensionWebSocketConnectionProvider ExtensionConnectionProvider { get; private set; }

        protected void IncrementItemStack(
            IGameData gameData,
            IPlayerInventoryProvider inventoryProvider,
            DataModels.GameSession session,
            Character character, Guid itemId)
        {
            var inventory = inventoryProvider.Get(character.Id);
            var items = inventory.AddItem(itemId);

            gameData.Add(gameData.CreateSessionEvent(GameEventType.ItemAdd, session, new ItemAdd
            {
                UserId = gameData.GetUser(character.UserId).UserId,
                Amount = 1,
                ItemId = itemId,
                InventoryItemId = items.FirstOrDefault().Id,
            }));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static DataModels.InventoryItem CreateInventoryItem(Character character, Guid itemId)
        {
            return new DataModels.InventoryItem { Id = Guid.NewGuid(), Amount = 1, CharacterId = character.Id, Equipped = false, ItemId = itemId };
        }

        public abstract void Process(
            IIntegrityChecker integrityChecker,
            IGameData gameData,
            IPlayerInventoryProvider inventoryProvider,
            DataModels.GameSession session,
            Character character,
            DataModels.CharacterState state);

        public void SetExtensionConnectionProvider(IExtensionWebSocketConnectionProvider provider)
        {
            this.ExtensionConnectionProvider = provider;
        }

        internal async Task<bool> TrySendToExtensionAsync<T>(Character character, T data)
        {
            if (character == null || data == null)
            {
                return false;
            }
            if (ExtensionConnectionProvider.TryGet(character.Id, out var connection))
            {
                try
                {
                    await connection.SendAsync(data);
                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

    }
}
