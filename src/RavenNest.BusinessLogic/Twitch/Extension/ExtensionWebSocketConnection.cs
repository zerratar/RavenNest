using Microsoft.Extensions.Logging;
using RavenNest.Sessions;
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Twitch.Extension
{
    public class ExtensionWebSocketConnection : IExtensionConnection
    {
        private readonly ILogger logger;
        private readonly System.Net.WebSockets.WebSocket socket;
        private readonly SessionInfo session;
        private readonly IExtensionPacketDataSerializer packetDataSerializer;
        private readonly byte[] buffer;
        private bool closed;

        public ExtensionWebSocketConnection(
            ILogger logger,
            WebSocket socket,
            IExtensionPacketDataSerializer serializer,
            SessionInfo session,
            string twitchBroadcasterId)
        {
            this.SendQueue = new ConcurrentQueue<Packet>();
            this.logger = logger;
            this.socket = socket;
            this.session = session;
            this.packetDataSerializer = serializer;
            this.buffer = new byte[4096];
            this.KillTask = new TaskCompletionSource<object>();
            this.BroadcasterTwitchUserId = twitchBroadcasterId;
        }

        public ConcurrentQueue<Packet> SendQueue { get; }

        public bool Closed => closed || this.socket.CloseStatus.HasValue;

        public Task KeepAlive()
        {
            return this.KillTask.Task;
        }

        public TaskCompletionSource<object> KillTask { get; set; }

        public SessionInfo Session => session;
        public string SessionId => session.SessionId;

        public string BroadcasterTwitchUserId { get; }

        public async Task<T> ReceiveAsync<T>()
        {
            var packet = await ReceiveAsync();
            if (packet != null)
            {
                return packetDataSerializer.Serialize<T>(packet);
            }

            return default(T);
        }

        public void Close()
        {
            this.KillTask.SetResult(null);
            this.closed = true;
            if (this.socket.State == WebSocketState.Connecting
                || this.socket.State == WebSocketState.Open
                || this.socket.State == WebSocketState.None)
            {
                try
                {
                    this.socket.CloseAsync(
                        WebSocketCloseStatus.Empty,
                        "Connection closed by server.",
                        CancellationToken.None);
                }
                catch (Exception exc)
                {
                    logger.LogError(exc.ToString());
                }
            }
        }

        public void EnqueueSend<T>(T data)
        {
            if (Closed)
            {
                return;
            }

            SendQueue.Enqueue(packetDataSerializer.Deserialize<T>(data));
        }

        public Task SendAsync<T>(T data)
        {
            return SendAsync(packetDataSerializer.Deserialize<T>(data));
        }

        public async Task SendAsync(Packet packet)
        {
            if (Closed)
            {
                return;
            }

            try
            {
                await socket.SendAsync(packet.Build(), packet.MessageType, packet.EndOfMessage, CancellationToken.None);
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                this.Close();
            }
        }

        internal async Task<Packet> ReceiveAsync()
        {
            if (Closed)
            {
                return null;
            }

            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.CloseStatus.HasValue)
            {
                this.closed = true;
                await this.socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                return null;
            }

            return packetDataSerializer.Deserialize(buffer, 0, result.Count, result.MessageType, result.EndOfMessage);
        }
    }
}
