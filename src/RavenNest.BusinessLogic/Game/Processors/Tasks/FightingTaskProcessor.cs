using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Providers;
using RavenNest.BusinessLogic.Twitch.Extension;
using RavenNest.DataModels;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class FightingTaskProcessor : ITaskProcessor
    {
        public IExtensionWebSocketConnectionProvider ExtensionConnectionProvider { get; private set; }

        public ITcpSocketApiConnectionProvider TcpConnectionProvider { get; private set; }
        public void Process(
            IIntegrityChecker integrityChecker,
            GameData gameData,
            PlayerInventoryProvider inventoryProvider,
            GameSession session,
            Character character,
            CharacterState state)
        {
        }

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
