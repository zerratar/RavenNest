using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using MessagePack;
using RavenNest.BusinessLogic.Net;
using RavenNest.Models;
using RavenNest.Models.TcpApi;

namespace RavenNest.LoadTest.Events;

/// <summary>
/// The game event socket (port 3920): Telepathy framing ([length:4 big endian][payload]), MessagePack
/// contractless. The client authenticates with a TypedPacket and then only receives EventList
/// messages. Connecting is also what starts the server's GameProcessor for the session, so a load
/// test without it would leave out real server work.
/// </summary>
public sealed class EventConnection : IAsyncDisposable
{
    private const int MaxMessageSize = 2_097_152 * 10; // TcpSocketApiConstants.MaxMessageSize
    private static readonly MessagePackSerializerOptions Contractless = MessagePack.Resolvers.ContractlessStandardResolver.Options;

    private readonly string host;
    private readonly int port;
    private readonly Func<string> sessionToken;
    private readonly Metrics metrics;
    private readonly Action<string> log;
    private CancellationTokenSource? cts;
    private Task? loop;

    public EventConnection(string host, int port, Func<string> sessionToken, Metrics metrics, Action<string> log)
    {
        this.host = host;
        this.port = port;
        this.sessionToken = sessionToken;
        this.metrics = metrics;
        this.log = log;
    }

    public volatile bool IsConnected;

    /// <summary>Connects and keeps reconnecting every 5 seconds after a drop, as the game's TcpApi does.</summary>
    public void Start(CancellationToken ct)
    {
        cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        loop = Task.Run(() => RunAsync(cts.Token));
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var first = true;
        while (!ct.IsCancellationRequested)
        {
            if (!first)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } catch (OperationCanceledException) { return; }
            }
            first = false;

            using var client = new TcpClient { NoDelay = true };
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(5));
                    if (IPAddress.TryParse(host, out var ip))
                        await client.ConnectAsync(ip, port, timeout.Token);
                    else
                        await client.ConnectAsync(host, port, timeout.Token);
                }
            }
            catch (Exception exc) when (exc is SocketException or OperationCanceledException)
            {
                if (ct.IsCancellationRequested) return;
                Interlocked.Increment(ref metrics.EventConnectFailures);
                log("event socket: connect failed: " + exc.Message);
                continue;
            }

            var stream = client.GetStream();
            try
            {
                await SendAsync(stream, BuildAuthPacket(sessionToken()), ct);
                Interlocked.Increment(ref metrics.EventConnects);
                IsConnected = true;
                await ReceiveLoopAsync(stream, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exc)
            {
                if (ct.IsCancellationRequested) return;
                log("event socket: " + exc.Message);
            }
            finally
            {
                IsConnected = false;
            }

            if (!ct.IsCancellationRequested)
            {
                Interlocked.Increment(ref metrics.EventDrops);
                log("event socket: disconnected, reconnecting in 5 s");
            }
        }
    }

    /// <summary>TcpApi.OnClientConnected: a TypedPacket carrying an AuthenticationRequest.</summary>
    public static byte[] BuildAuthPacket(string base64SessionToken)
    {
        var payload = MessagePackSerializer.Serialize(new AuthenticationRequest { SessionToken = base64SessionToken }, Contractless);
        var typed = new TypedPacket
        {
            MessageType = TcpMessageType.AuthenticationRequest,
            SessionToken = base64SessionToken,
            Payload = payload,
            Timestamp = DateTimeOffset.UtcNow
        };
        return MessagePackSerializer.Serialize(typed, Contractless);
    }

    private static async Task SendAsync(NetworkStream stream, byte[] message, CancellationToken ct)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, message.Length);
        await stream.WriteAsync(header, ct);
        await stream.WriteAsync(message, ct);
        await stream.FlushAsync(ct);
    }

    private async Task ReceiveLoopAsync(NetworkStream stream, CancellationToken ct)
    {
        var header = new byte[4];
        while (!ct.IsCancellationRequested)
        {
            if (!await ReadExactAsync(stream, header, 4, ct))
            {
                return;
            }

            var size = BinaryPrimitives.ReadInt32BigEndian(header);
            if (size <= 0 || size > MaxMessageSize)
            {
                throw new InvalidDataException("bad message size " + size);
            }

            var data = new byte[size];
            if (!await ReadExactAsync(stream, data, size, ct))
            {
                return;
            }

            Interlocked.Increment(ref metrics.EventBatches);
            Interlocked.Add(ref metrics.EventBytes, size + 4);
            try
            {
                var list = MessagePackSerializer.Deserialize<EventList>(data, Contractless);
                if (list?.Events == null) continue;
                foreach (var e in list.Events)
                {
                    Interlocked.Increment(ref metrics.EventCount);
                    metrics.CountEvent(Enum.IsDefined(typeof(GameEventType), e.Type) ? ((GameEventType)e.Type).ToString() : "Type" + e.Type);
                }
            }
            catch (Exception exc)
            {
                Interlocked.Increment(ref metrics.EventDecodeFailures);
                log("event socket: could not decode an event list: " + exc.Message);
            }
        }
    }

    private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken ct)
    {
        var read = 0;
        while (read < count)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read, count - read), ct);
            if (n <= 0) return false;
            read += n;
        }
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        try { cts?.Cancel(); } catch { }
        if (loop != null)
        {
            try { await loop.WaitAsync(TimeSpan.FromSeconds(3)); } catch { }
        }
    }
}
