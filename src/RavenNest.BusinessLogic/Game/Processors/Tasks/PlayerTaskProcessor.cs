using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Twitch.Extension;
using RavenNest.DataModels;
using RavenNest.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public abstract class PlayerTaskProcessor : ITaskProcessor
    {
        public readonly Random Random = new Random();
        public ITwitchExtensionConnectionProvider ExtensionConnectionProvider { get; private set; }
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
            ILogger logger,
            GameData gameData,
            PlayerInventoryProvider inventoryProvider,
            DataModels.GameSession session,
            Character character,
            DataModels.CharacterState state);

        public void SetExtensionConnectionProvider(ITwitchExtensionConnectionProvider provider)
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
