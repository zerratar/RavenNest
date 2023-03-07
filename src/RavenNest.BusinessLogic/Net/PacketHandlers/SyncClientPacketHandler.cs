using RavenNest.BusinessLogic.Game;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Net
{
    internal class SyncClientPacketHandler : IGamePacketHandler
    {
        private readonly SessionManager sessionManager;
        public SyncClientPacketHandler(SessionManager sessionManager)
        {
            this.sessionManager = sessionManager;
        }

        public async Task HandleAsync(IGameWebSocketConnection connection, GamePacket packet)
        {
            if (packet.Data is ClientSyncUpdate update)
            {
                this.sessionManager.UpdateSessionState(connection.SessionToken, update);
            }
        }
    }
}
