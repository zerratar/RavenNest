using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.Extensions.Logging;
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
        private readonly WebSocket socket;
        private readonly RavenNest.Models.SessionInfo session;
        private readonly IExtensionPacketDataSerializer packetDataSerializer;
        private readonly byte[] buffer;
        private bool closed;

        public ExtensionWebSocketConnection(
            ILogger logger,
            WebSocket socket,
            IExtensionPacketDataSerializer serializer,
            RavenNest.Models.SessionInfo session,
            string twitchBroadcasterId)
        {
            this.SendQueue = new ConcurrentQueue<Packet>();
            this.logger = logger;
            this.socket = socket;
            this.session = session;
            this.packetDataSerializer = serializer;
            this.buffer = new byte[4096];
            this.BroadcasterTwitchUserId = twitchBroadcasterId;
        }

        public ConcurrentQueue<Packet> SendQueue { get; }

        public bool Closed => closed || this.socket.CloseStatus.HasValue;

        public async Task KeepAlive(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (Closed)
                {
                    return;
                }

                while (SendQueue.TryDequeue(out var packet))
                {
                    try
                    {
                        await SendAsync(packet);
                    }
                    catch (Exception exc)
                    {
                        logger.LogError(exc.ToString());
                        //this.Close();
                    }
                }

                await Task.Delay(1000);
            }
        }

        public TaskCompletionSource<object> KillTask { get; set; }

        public RavenNest.Models.SessionInfo Session => session;
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
                    // ignored
                    // logger.LogError(exc.ToString());
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

        private bool TryBuildPacket(Packet packet, out ArraySegment<byte> data)
        {
            try
            {
                data = packet.Build();
                return true;
            }
            catch (Exception exc)
            {
                data = ArraySegment<byte>.Empty;
                logger.LogError(exc.ToString());
                return false;
            }
        }

        public async Task SendAsync(Packet packet)
        {
            if (Closed)
            {
                return;
            }

            if (packet == null)
            {
                return;
            }

            try
            {
                if(socket.State != WebSocketState.Open)
                {
                    this.Close();
                    return;
                }

                if (packet.MessageType == WebSocketMessageType.Close)
                {
                    this.Close();
                    return;
                }

                if (TryBuildPacket(packet, out var data))
                {
                    await socket.SendAsync(data, packet.MessageType, packet.EndOfMessage, CancellationToken.None);
                }
            }
            catch (WebSocketException exc)
            {
                this.Close();
            }
            catch (Exception exc)
            {
                //logger.LogError(exc.ToString());
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
