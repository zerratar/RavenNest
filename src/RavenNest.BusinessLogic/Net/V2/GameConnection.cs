using System;
using System.Buffers;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RavenNest.Protocol;

namespace RavenNest.BusinessLogic.Net.V2
{
    /// <summary>
    /// One game's WebSocket. A single loop reads whole messages and hands them on; a single loop writes, from a
    /// queue, so nothing ever writes to the socket from two threads. Knows nothing about sessions.
    /// </summary>
    public sealed class GameConnection
    {
        private static int lastId;

        private readonly WebSocket socket;
        private readonly ILogger logger;
        private readonly Channel<byte[]> outbox = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(10_000)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });
        private readonly CancellationTokenSource closing = new CancellationTokenSource();

        public GameConnection(WebSocket socket, string remote, ILogger logger)
        {
            this.socket = socket;
            this.logger = logger;
            Remote = remote;
            // negative, so they never clash with the TCP api's connection ids in the game loop's bookkeeping
            Id = -Interlocked.Increment(ref lastId);
        }

        public int Id { get; }
        public string Remote { get; }

        /// <summary>When the last message (of any kind) arrived, for spotting a dead connection.</summary>
        public DateTime LastReceived { get; private set; } = DateTime.UtcNow;

        /// <summary>When anything was last sent, so pings only go when nothing else did.</summary>
        public DateTime LastSent { get; private set; } = DateTime.UtcNow;

        public bool IsOpen => socket.State == WebSocketState.Open && !closing.IsCancellationRequested;

        public long MessagesIn, MessagesOut, BytesIn, BytesOut;

        /// <summary>
        /// The largest message this game reads: 4 MB until its Hello shows a game that reads more
        /// (<see cref="ProtocolInfo.LargeMessagesFrom"/>).
        /// </summary>
        public int MaxSendBytes { get; set; } = ProtocolInfo.LegacyMaxMessageBytes;

        /// <summary>
        /// Queues a message. False when the connection is closing or its queue is full (a client that stopped
        /// reading); the caller keeps whatever needs to survive (events stay in the log, saves stay unacknowledged).
        /// </summary>
        public bool Send(IMessage message)
        {
            if (!IsOpen)
            {
                return false;
            }

            byte[] data;
            try
            {
                data = ProtocolSerializer.Serialize(message);
            }
            catch (Exception exc)
            {
                logger.LogError("Could not serialize a v2 " + message.GetType().Name + ": " + exc);
                return false;
            }

            // A game closes the connection on a message over what it reads and asks again after reconnecting, so an
            // answer that big (a restore of 600 players to a 0.9.5.1a game) dropped the link over and over. Refused
            // instead: the game goes back to the old connection, which has room for it.
            if (data.Length > MaxSendBytes && message is not Rejected)
            {
                logger.LogWarning("v2 connection " + Id + ": a " + message.GetType().Name + " of " + data.Length + " bytes is over the limit; sending the game to the old connection.");
                Reject(new Rejected { Reason = RejectReason.MessageTooLarge, Message = "An answer was too large for this connection; use the old one." });
                return false;
            }

            if (!outbox.Writer.TryWrite(data))
            {
                logger.LogWarning("v2 connection " + Id + " is not reading; closing it.");
                Close(WebSocketCloseStatus.PolicyViolation, "send queue full");
                return false;
            }
            return true;
        }

        /// <summary>Sends <paramref name="rejected"/>, then closes once it is written.</summary>
        public void Reject(Rejected rejected)
        {
            Send(rejected);
            outbox.Writer.TryComplete();
        }

        public void Close(WebSocketCloseStatus status, string reason)
        {
            if (closing.IsCancellationRequested)
            {
                return;
            }

            outbox.Writer.TryComplete();
            _ = CloseAsync(status, reason);
        }

        private async Task CloseAsync(WebSocketCloseStatus status, string reason)
        {
            try
            {
                if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await socket.CloseOutputAsync(status, reason, timeout.Token);
                }
            }
            catch { }
            finally
            {
                closing.Cancel();
            }
        }

        /// <summary>
        /// Reads until the socket closes, calling <paramref name="onMessage"/> for every message on this thread, in
        /// order. Returns when the connection is over; writes stop then too.
        /// </summary>
        public async Task RunAsync(Func<GameConnection, IMessage, Task> onMessage, CancellationToken ct)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, closing.Token);
            var writer = Task.Run(() => WriteLoopAsync(linked.Token));
            var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
            var message = new MemoryStream();
            try
            {
                while (!linked.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), linked.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    message.Write(buffer, 0, result.Count);
                    // every part counts: a big message can take longer to arrive than DeadAfterMs
                    LastReceived = DateTime.UtcNow;
                    if (message.Length > ProtocolInfo.MaxMessageBytes)
                    {
                        Reject(new Rejected { Reason = RejectReason.MessageTooLarge, Message = "Messages are limited to " + ProtocolInfo.MaxMessageBytes + " bytes.", Reconnect = true });
                        break;
                    }

                    if (!result.EndOfMessage)
                    {
                        continue;
                    }

                    LastReceived = DateTime.UtcNow;
                    Interlocked.Increment(ref MessagesIn);
                    Interlocked.Add(ref BytesIn, message.Length);

                    var data = new ReadOnlyMemory<byte>(message.GetBuffer(), 0, (int)message.Length);
                    message = new MemoryStream();
                    if (result.MessageType != WebSocketMessageType.Binary)
                    {
                        continue;
                    }

                    IMessage msg;
                    try
                    {
                        if (!ProtocolSerializer.TryDeserialize(data, out msg))
                        {
                            continue; // a message type from a newer client: skip it
                        }
                    }
                    catch (Exception exc)
                    {
                        logger.LogWarning("v2 connection " + Id + " sent a message that could not be read: " + exc.Message);
                        continue;
                    }

                    await onMessage(this, msg);
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException) { }
            catch (Exception exc)
            {
                logger.LogError("v2 connection " + Id + " failed: " + exc);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                outbox.Writer.TryComplete();
                try { await writer.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
                if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                {
                    await CloseAsync(WebSocketCloseStatus.NormalClosure, "bye");
                }
                closing.Cancel();
            }
        }

        private async Task WriteLoopAsync(CancellationToken ct)
        {
            try
            {
                while (await outbox.Reader.WaitToReadAsync(ct))
                {
                    while (outbox.Reader.TryRead(out var data))
                    {
                        await socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, ct);
                        LastSent = DateTime.UtcNow;
                        Interlocked.Increment(ref MessagesOut);
                        Interlocked.Add(ref BytesOut, data.Length);
                    }
                }

                // completed: a Reject was the last message; close now that it is out
                await CloseAsync(WebSocketCloseStatus.NormalClosure, "closed by server");
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException) { }
            catch (Exception exc)
            {
                logger.LogError("v2 connection " + Id + " could not send: " + exc);
                closing.Cancel();
            }
        }
    }
}
