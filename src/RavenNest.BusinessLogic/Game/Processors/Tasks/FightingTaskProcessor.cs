using Microsoft.Extensions.Logging;
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
        public ITwitchExtensionConnectionProvider ExtensionConnectionProvider { get; private set; }

        public ITcpSocketApiConnectionProvider TcpConnectionProvider { get; private set; }
        public void Process(
            ILogger logger,
            GameData gameData,
            PlayerInventory inventoryProvider,
            GameSession session,
            User user,
            Character character,
            CharacterState state)
        {
        }

        public void SetExtensionConnectionProvider(ITwitchExtensionConnectionProvider provider)
        {
            this.ExtensionConnectionProvider = provider;
        }

        public void SetTcpSocketApiConnectionProvider(ITcpSocketApiConnectionProvider provider)
        {
            this.TcpConnectionProvider = provider;
        }
    }
}
