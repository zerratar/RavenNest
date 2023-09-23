using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Twitch.Extension;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public interface ITaskProcessor
    {
        void Process(
            ILogger logger,
            GameData gameData,
            PlayerInventoryProvider inventoryProvider,
            GameSession session,
            Character character,
            CharacterState state);


        // fugly
        void SetExtensionConnectionProvider(ITwitchExtensionConnectionProvider provider);

        // but it aint stupid if it works? ahem.. I'm sure this is just stupid. Since we don't register the TaskProcessors with the IOC we cant use dependency injection.
        void SetTcpSocketApiConnectionProvider(ITcpSocketApiConnectionProvider provider);
    }
}
