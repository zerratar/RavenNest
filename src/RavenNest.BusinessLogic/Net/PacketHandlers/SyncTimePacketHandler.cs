using RavenNest.BusinessLogic.Game;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Net
{
    internal class SyncTimePacketHandler : IGamePacketHandler
    {
        private readonly ISessionManager sessionManager;

        public SyncTimePacketHandler(ISessionManager sessionManager)
        {
            this.sessionManager = sessionManager;
        }

        public async Task HandleAsync(IGameWebSocketConnection connection, GamePacket packet)
        {
            if (packet.Data is TimeSyncUpdate update)
            {
                this.sessionManager.RecordTimeMismatch(connection.SessionToken, update);
            }
        }
    }
}
