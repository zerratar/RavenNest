using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
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
        public readonly Random Random = new Random();
        public IExtensionWebSocketConnectionProvider ExtensionConnectionProvider { get; private set; }
        public ITcpSocketApiConnectionProvider TcpConnectionProvider { get; private set; }

        public void IncrementItemStack(
            GameData gameData,
            PlayerInventoryProvider inventoryProvider,
            DataModels.GameSession session,
            Character character, Guid itemId)
        {
            var inventory = inventoryProvider.Get(character.Id);
            var items = inventory.AddItem(itemId);

            gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.ItemAdd, session, new ItemAdd
            {
                PlayerId = character.Id,
                Amount = 1,
                ItemId = itemId,
                InventoryItemId = items.FirstOrDefault().Id,
            }));
        }

        public abstract void Process(
            IIntegrityChecker integrityChecker,
            GameData gameData,
            PlayerInventoryProvider inventoryProvider,
            DataModels.GameSession session,
            Character character,
            DataModels.CharacterState state);

        public void SetExtensionConnectionProvider(IExtensionWebSocketConnectionProvider provider)
        {
            this.ExtensionConnectionProvider = provider;
        }

        public void SetTcpSocketApiConnectionProvider(ITcpSocketApiConnectionProvider provider)
        {
            this.TcpConnectionProvider = provider;
        }

        internal async Task<bool> TrySendToExtensionAsync<T>(Character character, T data)
        {
            if (character == null || data == null || ExtensionConnectionProvider == null)
            {
                return false;
            }

            try
            {
                if (ExtensionConnectionProvider.TryGet(character.Id, out var connection))
                {
                    await connection.SendAsync(data);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

    }
}
