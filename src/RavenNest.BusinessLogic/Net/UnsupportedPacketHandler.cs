using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Net
{
    internal class UnsupportedPacketHandler : IGamePacketHandler
    {
        private readonly ILogger logger;

        public UnsupportedPacketHandler(ILogger logger)
        {
            this.logger = logger;
        }

        public Task HandleAsync(
            IWebSocketConnection connection, 
            GamePacket packet)
        {
            return logger.WriteErrorAsync($"Unsupported packet received with id: {packet.Id}. payload type: {packet.Type}");
        }
    }
}