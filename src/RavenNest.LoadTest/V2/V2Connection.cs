using System.Buffers;
using System.Net;
using System.Net.Security;
using System.Net.WebSockets;
using System.Threading.Channels;
using RavenNest.LoadTest.Rest;
using RavenNest.Protocol;

namespace RavenNest.LoadTest.V2;

/// <summary>
/// One v2 game connection (wss://host/api/v2/game): a WebSocket carrying RavenNest.Protocol messages, one
/// binary message each. One loop reads and hands each message on, in order; one loop writes from a queue,
/// so nothing writes to the socket from two threads (the same shape as the server's GameConnection).
/// Knows nothing about saves or events.
/// </summary>
public sealed class V2Connection : IAsyncDisposable
{
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);

    private readonly ClientWebSocket socket;
    private readonly Metrics metrics;
    private readonly Channel<byte[]> outbox = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource life = new();
    private Action<IMessage, int>? onMessage;
    private Task? reader, writer;

    private V2Connection(ClientWebSocket socket, Metrics metrics)
    {
        this.socket = socket;
        this.metrics = metrics;
    }

    public DateTime LastReceived { get; private set; } = DateTime.UtcNow;
    public DateTime LastSent { get; private set; } = DateTime.UtcNow;

    /// <summary>True from Welcome until the socket closes or is aborted.</summary>
    public bool IsOpen => socket.State == WebSocketState.Open && !life.IsCancellationRequested;

    /// <summary>Set when this side aborted it (chaos or shutdown), so the drop is not counted as the server's.</summary>
    public bool AbortedByUs { get; private set; }

    /// <summary>Why the read loop ended, for the log.</summary>
    public string? CloseReason { get; private set; }

    /// <summary>Completes when the read loop has ended.</summary>
    public Task Completion => reader ?? Task.CompletedTask;

    /// <summary>
    /// The handler the game's network thread would have: accepts the local development certificate and
    /// connects over IPv4, like the REST client (see <see cref="RestApi"/>).
    /// </summary>
    public static HttpMessageInvoker CreateInvoker()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = RestApi.ConnectIPv4Async,
            UseCookies = false,
            SslOptions = new SslClientAuthenticationOptions
            {
                // the host was checked to be this machine (see Safety) before anything is sent
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            }
        };
        return new HttpMessageInvoker(handler);
    }

    public static Uri EndpointFor(Uri baseUrl)
    {
        var b = new UriBuilder(baseUrl)
        {
            Scheme = baseUrl.Scheme == Uri.UriSchemeHttp ? "ws" : "wss",
            Path = ProtocolInfo.Path
        };
        if (baseUrl.IsDefaultPort) b.Port = -1;
        return b.Uri;
    }

    /// <summary>
    /// Opens the socket, says Hello and waits for the answer. Returns the connection with its Welcome, or
    /// null with the Rejected message or an error. Nothing more is read until <see cref="Start"/>, so the
    /// caller can act on the Welcome before the first event arrives.
    /// </summary>
    public static async Task<(V2Connection? Connection, Welcome? Welcome, Rejected? Rejected, string? Error)> ConnectAsync(
        Uri endpoint, HttpMessageInvoker invoker, Hello hello, Metrics metrics, CancellationToken ct)
    {
        var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.Zero; // the protocol has its own Ping
        var c = new V2Connection(socket, metrics);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(HandshakeTimeout);
        try
        {
            await socket.ConnectAsync(endpoint, invoker, timeout.Token);
            var helloBytes = ProtocolSerializer.Serialize(hello);
            await socket.SendAsync(helloBytes, WebSocketMessageType.Binary, true, timeout.Token);
            c.CountOut(helloBytes.Length);

            // The first answer is Welcome or Rejected. Anything else before it (there should be nothing) is skipped.
            while (true)
            {
                var (message, size) = await c.ReceiveOneAsync(timeout.Token);
                if (message == null)
                {
                    socket.Dispose();
                    return (null, null, null, "the server closed the connection before answering Hello" + (c.CloseReason != null ? " (" + c.CloseReason + ")" : ""));
                }

                if (message is Welcome welcome)
                {
                    return (c, welcome, null, null);
                }

                if (message is Rejected rejected)
                {
                    socket.Dispose();
                    return (null, null, rejected, null);
                }
            }
        }
        catch (Exception exc) when (exc is WebSocketException or OperationCanceledException or HttpRequestException or IOException)
        {
            socket.Dispose();
            var why = exc is OperationCanceledException && !ct.IsCancellationRequested
                ? "no answer to Hello within " + HandshakeTimeout.TotalSeconds + " s"
                : (exc.InnerException?.Message ?? exc.Message);
            return (null, null, null, why);
        }
    }

    /// <summary>Starts reading (every message after the Welcome goes to <paramref name="handler"/>, in order) and writing.</summary>
    public void Start(Action<IMessage, int> handler)
    {
        onMessage = handler;
        reader = Task.Run(ReadLoopAsync);
        writer = Task.Run(WriteLoopAsync);
    }

    /// <summary>Queues a message. False when the connection is closed (the caller keeps what must survive).</summary>
    public bool Send(IMessage message)
    {
        if (!IsOpen)
        {
            return false;
        }
        return outbox.Writer.TryWrite(ProtocolSerializer.Serialize(message));
    }

    /// <summary>Drops the connection without a close handshake, as a lost network would.</summary>
    public void Abort()
    {
        AbortedByUs = true;
        try { socket.Abort(); } catch { }
        try { life.Cancel(); } catch { }
        outbox.Writer.TryComplete();
    }

    private void CountOut(int bytes)
    {
        LastSent = DateTime.UtcNow;
        Interlocked.Increment(ref metrics.V2MessagesOut);
        Interlocked.Add(ref metrics.V2BytesOut, bytes);
    }

    private async Task<(IMessage? Message, int Size)> ReceiveOneAsync(CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        var message = new MemoryStream();
        try
        {
            while (true)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    CloseReason = socket.CloseStatus + (string.IsNullOrEmpty(socket.CloseStatusDescription) ? "" : ": " + socket.CloseStatusDescription);
                    return (null, 0);
                }

                message.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage)
                {
                    continue;
                }

                LastReceived = DateTime.UtcNow;
                var size = (int)message.Length;
                Interlocked.Increment(ref metrics.V2MessagesIn);
                Interlocked.Add(ref metrics.V2BytesIn, size);
                if (result.MessageType != WebSocketMessageType.Binary)
                {
                    message.SetLength(0);
                    continue;
                }

                try
                {
                    if (ProtocolSerializer.TryDeserialize(new ReadOnlyMemory<byte>(message.GetBuffer(), 0, size), out var msg))
                    {
                        return (msg, size);
                    }
                }
                catch (Exception exc)
                {
                    Interlocked.Increment(ref metrics.V2DecodeFailures);
                    metrics.CountV2("decode failure: " + exc.GetType().Name);
                }
                message.SetLength(0);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            while (!life.IsCancellationRequested)
            {
                var (message, size) = await ReceiveOneAsync(life.Token);
                if (message == null)
                {
                    break;
                }
                onMessage?.Invoke(message, size);
            }
        }
        catch (OperationCanceledException) { CloseReason ??= "aborted"; }
        catch (WebSocketException exc) { CloseReason ??= exc.Message; }
        catch (Exception exc) { CloseReason ??= "read loop failed: " + exc.Message; }
        finally
        {
            outbox.Writer.TryComplete();
            try { life.Cancel(); } catch { }
        }
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            while (await outbox.Reader.WaitToReadAsync(life.Token))
            {
                while (outbox.Reader.TryRead(out var data))
                {
                    await socket.SendAsync(data, WebSocketMessageType.Binary, true, life.Token);
                    CountOut(data.Length);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        catch (ObjectDisposedException) { }
        finally
        {
            try { life.Cancel(); } catch { }
        }
    }

    /// <summary>Closes politely (when the run ends), then lets go of the socket.</summary>
    public async ValueTask DisposeAsync()
    {
        if (socket.State == WebSocketState.Open && !AbortedByUs)
        {
            AbortedByUs = true;
            outbox.Writer.TryComplete();
            if (writer != null)
            {
                try { await writer.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
            }
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "run over", timeout.Token);
            }
            catch { }
        }
        try { life.Cancel(); } catch { }
        if (reader != null)
        {
            try { await reader.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
        }
        socket.Dispose();
    }
}
