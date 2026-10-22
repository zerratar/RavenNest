using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace RavenNest.LoadTest.Delta;

/// <summary>
/// The delta socket (port 3921): one per streamer session, authenticated with the session token
/// string, then experience, player state and game state frames. The server never writes back, so a
/// read that returns is the server closing the connection.
/// </summary>
public sealed class DeltaConnection : IAsyncDisposable
{
    private readonly string host;
    private readonly int port;
    private readonly Func<byte[]> authPayload;
    private readonly Metrics metrics;
    private readonly Action<string> log;
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private Socket? socket;
    private CancellationTokenSource? readCts;
    private volatile bool connected;

    public DeltaConnection(string host, int port, Func<byte[]> authPayload, Metrics metrics, Action<string> log)
    {
        this.host = host;
        this.port = port;
        this.authPayload = authPayload;
        this.metrics = metrics;
        this.log = log;
    }

    public bool Connected => connected && socket != null && socket.Connected;

    /// <summary>Raised when the server closed the connection or a send failed.</summary>
    public event Action? Disconnected;

    public async Task<bool> ConnectAsync(CancellationToken ct)
    {
        await sendLock.WaitAsync(ct);
        try
        {
            CloseSocket();

            // Nagle stays on (no NoDelay), as in the game's DeltaClient.
            var s = new Socket(SocketType.Stream, ProtocolType.Tcp);
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    if (IPAddress.TryParse(host, out var ip))
                    {
                        await s.ConnectAsync(new IPEndPoint(ip, port), timeout.Token);
                    }
                    else
                    {
                        await s.ConnectAsync(host, port, timeout.Token);
                    }
                }
                catch (Exception exc) when (exc is SocketException or OperationCanceledException)
                {
                    s.Dispose();
                    Interlocked.Increment(ref metrics.DeltaConnectFailures);
                    if (!ct.IsCancellationRequested)
                    {
                        log("delta socket: connect failed: " + exc.Message);
                    }
                    return false;
                }
            }

            socket = s;
            var auth = DeltaWriter.Frame(DeltaFrameType.Auth, authPayload());
            await s.SendAsync(auth, SocketFlags.None, ct);
            Interlocked.Increment(ref metrics.DeltaAuthFrames);
            Interlocked.Add(ref metrics.DeltaBytes, auth.Length);
            Interlocked.Increment(ref metrics.DeltaConnects);
            connected = true;

            readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _ = Task.Run(() => WatchForCloseAsync(s, readCts.Token));
            return true;
        }
        catch (Exception exc) when (exc is SocketException or ObjectDisposedException or IOException)
        {
            Interlocked.Increment(ref metrics.DeltaConnectFailures);
            log("delta socket: auth send failed: " + exc.Message);
            CloseSocket();
            return false;
        }
        finally
        {
            sendLock.Release();
        }
    }

    private async Task WatchForCloseAsync(Socket s, CancellationToken ct)
    {
        var buffer = new byte[256];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var n = await s.ReceiveAsync(buffer, SocketFlags.None, ct);
                if (n <= 0)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            // reset or disposed: same as a close
        }

        if (!ct.IsCancellationRequested && ReferenceEquals(s, socket))
        {
            OnDropped("server closed the delta connection");
        }
    }

    /// <summary>Writes one frame. False when the socket is down or the write failed.</summary>
    public async Task<bool> SendFrameAsync(byte type, byte[] payload, int length, CancellationToken ct)
    {
        if (length == 0)
        {
            return true;
        }

        var frame = DeltaWriter.Frame(type, payload.AsSpan(0, length));
        await sendLock.WaitAsync(ct);
        try
        {
            var s = socket;
            if (s == null || !connected || !s.Connected)
            {
                Interlocked.Increment(ref metrics.DeltaSendFailures);
                return false;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var sent = 0;
                while (sent < frame.Length)
                {
                    sent += await s.SendAsync(frame.AsMemory(sent), SocketFlags.None, ct);
                }
                metrics.DeltaSend.Record(sw.Elapsed.TotalMilliseconds, true);
                Interlocked.Add(ref metrics.DeltaBytes, frame.Length);
                return true;
            }
            catch (Exception exc) when (exc is SocketException or ObjectDisposedException or IOException)
            {
                metrics.DeltaSend.Record(sw.Elapsed.TotalMilliseconds, false, exc.Message);
                Interlocked.Increment(ref metrics.DeltaSendFailures);
                OnDropped("delta send failed: " + exc.Message);
                return false;
            }
        }
        finally
        {
            sendLock.Release();
        }
    }

    private void OnDropped(string why)
    {
        if (!connected)
        {
            return;
        }

        connected = false;
        Interlocked.Increment(ref metrics.DeltaDrops);
        log("delta socket: " + why);
        Disconnected?.Invoke();
    }

    private void CloseSocket()
    {
        connected = false;
        try { readCts?.Cancel(); } catch { }
        readCts = null;
        var s = socket;
        socket = null;
        if (s != null)
        {
            try { s.Shutdown(SocketShutdown.Both); } catch { }
            try { s.Dispose(); } catch { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await sendLock.WaitAsync();
        try
        {
            CloseSocket();
        }
        finally
        {
            sendLock.Release();
        }
    }

    /// <summary>What the game puts in the auth frame: UTF-8 of base64(JSON(session token)), see ModelExtensions.ToBytes.</summary>
    public static byte[] AuthPayload(string base64SessionToken) => Encoding.UTF8.GetBytes(base64SessionToken);
}
