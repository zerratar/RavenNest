using RavenNest.Models;
using MessagePack;

namespace RavenNest.BusinessLogic.Net
{
    public class TcpSocketApiConnection
    {
        private readonly int connectionId;
        private TcpSocketApi server;

        public TcpSocketApiConnection(int connectionId, TcpSocketApi server)
        {
            this.connectionId = connectionId;
            this.server = server;
        }
        public SessionToken SessionToken { get; set; }
        public bool Connected => server.IsConnected(this.connectionId);
        public void Send<T>(T model)
        {
            // send a separate packet first with the incoming model type name?
            // or just let it be?

            // currently we should only be sending RavenNest.Models.PlayerRestedUpdate, but this may change in the future.
            server.Send(connectionId, MessagePackSerializer.Serialize(model, MessagePack.Resolvers.ContractlessStandardResolver.Options));
        }
    }
}
