using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RavenNest.Models;

namespace RavenNest.LoadTest.Rest;

public sealed class ApiResult
{
    public bool Ok { get; init; }
    public HttpStatusCode Status { get; init; }
    public string Body { get; init; } = "";
    public string? Error { get; init; }
    public double Milliseconds { get; init; }

    public JToken? Json()
    {
        if (string.IsNullOrWhiteSpace(Body)) return null;
        try { return JToken.Parse(Body); } catch { return null; }
    }

    public T? As<T>()
    {
        if (string.IsNullOrWhiteSpace(Body)) return default;
        try { return JsonConvert.DeserializeObject<T>(Body); } catch { return default; }
    }
}

/// <summary>
/// The REST side, sent the way the game's WebApiRequest sends it: the tokens go base64 encoded JSON
/// in the auth-token and session-token headers, bodies are JSON.
/// </summary>
public sealed class RestApi : IDisposable
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/77.0.3865.90 Safari/537.36";

    private readonly Options options;
    private readonly Metrics metrics;
    private readonly HttpClient? shared;
    private readonly Uri apiBase;

    public RestApi(Options options, Metrics metrics)
    {
        this.options = options;
        this.metrics = metrics;
        apiBase = new Uri(options.BaseUrl.ToString().TrimEnd('/') + "/api/");
        if (!options.NewConnectionPerRequest)
        {
            shared = CreateClient(pooled: true);
        }
    }

    public string? AuthTokenHeader { get; set; }
    public string? SessionTokenHeader { get; set; }

    public static string Encode(object token) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(token)));

    private HttpClient CreateClient(bool pooled)
    {
        var handler = new SocketsHttpHandler
        {
            MaxConnectionsPerServer = pooled ? 512 : 1,
            PooledConnectionLifetime = pooled ? TimeSpan.FromMinutes(10) : TimeSpan.Zero,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false,
            // localhost resolves to ::1 first and the server listens on IPv4 only, so every new connection
            // would wait about 2 s for Windows to give up on IPv6. The game connects over IPv4.
            ConnectCallback = ConnectIPv4Async,
            SslOptions = new SslClientAuthenticationOptions
            {
                // The local server uses a development certificate. The host was checked to be this
                // machine (see Safety) before anything is sent, as the game's own handler also accepts it.
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            }
        };
        return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    private static async ValueTask<Stream> ConnectIPv4Async(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var host = context.DnsEndPoint.Host;
        var address = IPAddress.TryParse(host, out var ip) ? ip
            : (await Dns.GetHostAddressesAsync(host, AddressFamily.InterNetwork, ct)).First();
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Sends a request and records it under <paramref name="endpoint"/>. The path is relative to
    /// /api/. Nothing secret ends up in the endpoint name or the failure text.
    /// </summary>
    public async Task<ApiResult> SendAsync(string endpoint, HttpMethod method, string path, object? body, CancellationToken ct, bool record = true)
    {
        using var request = new HttpRequestMessage(method, new Uri(apiBase, path));
        request.Headers.UserAgent.ParseAdd(UserAgent);
        if (AuthTokenHeader != null) request.Headers.Add("auth-token", AuthTokenHeader);
        if (SessionTokenHeader != null) request.Headers.Add("session-token", SessionTokenHeader);
        if (body != null)
        {
            request.Content = new StringContent(body as string ?? JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
        }

        var client = shared ?? CreateClient(pooled: false);
        var sw = Stopwatch.StartNew();
        ApiResult result;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));
            using var response = await client.SendAsync(request, timeout.Token);
            var text = await response.Content.ReadAsStringAsync(timeout.Token);
            sw.Stop();
            var ok = response.StatusCode == HttpStatusCode.OK;
            result = new ApiResult
            {
                Ok = ok,
                Status = response.StatusCode,
                Body = text,
                Error = ok ? null : "HTTP " + (int)response.StatusCode + " " + response.StatusCode + Excerpt(text),
                Milliseconds = sw.Elapsed.TotalMilliseconds
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            result = new ApiResult { Ok = false, Error = "timed out after " + options.RequestTimeoutSeconds + " s", Milliseconds = sw.Elapsed.TotalMilliseconds };
        }
        catch (HttpRequestException exc)
        {
            sw.Stop();
            result = new ApiResult { Ok = false, Error = "request failed: " + (exc.InnerException?.Message ?? exc.Message), Milliseconds = sw.Elapsed.TotalMilliseconds };
        }
        finally
        {
            if (shared == null)
            {
                client.Dispose();
            }
        }

        if (record)
        {
            metrics.Endpoint(endpoint).Record(result.Milliseconds, result.Ok, result.Error);
        }
        return result;
    }

    /// <summary>Records an application level outcome (HTTP 200 but the server said no) for an endpoint.</summary>
    public void RecordOutcome(string endpoint, ApiResult result, bool success, string? failure)
    {
        metrics.Endpoint(endpoint).Record(result.Milliseconds, success, failure);
    }

    public void Dispose() => shared?.Dispose();

    private static string Excerpt(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        text = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return ": " + (text.Length > 120 ? text[..120] + "..." : text);
    }

    // ---- the calls the game makes ----

    /// <summary>AuthApi.AuthenticateAsync: POST api/auth {"Username","Password"}.</summary>
    public async Task<(AuthToken? Token, string? Error)> AuthenticateAsync(string username, string password, CancellationToken ct)
    {
        var r = await SendAsync("POST auth", HttpMethod.Post, "auth", new { Username = username, Password = password }, ct);
        if (!r.Ok) return (null, r.Error);
        var token = r.As<AuthToken>();
        if (token == null || token.UserId == Guid.Empty || string.IsNullOrEmpty(token.Token))
        {
            return (null, "the server refused the user name or password");
        }
        return (token, null);
    }

    /// <summary>GameApi.BeginSessionAsync: POST api/game/{clientVersion}/{accessKey}/{syncTime}.</summary>
    public async Task<(BeginSessionResult? Result, string? Error)> BeginSessionAsync(string clientVersion, string accessKey, CancellationToken ct)
    {
        // the access key goes in the path, as the game sends it; it never goes in a log line
        var path = "game/" + Uri.EscapeDataString(clientVersion) + "/" + Uri.EscapeDataString(accessKey) + "/0";
        var r = await SendAsync("POST game/begin-session", HttpMethod.Post, path, null, ct);
        if (!r.Ok)
        {
            return (null, r.Status == HttpStatusCode.Forbidden ? "HTTP 403 (bad auth token?)" : r.Error);
        }
        // Only the parts needed, so a change in the village or settings models cannot break this.
        var json = r.Json() as JObject;
        var state = (BeginSessionResultState?)json?.Get("state")?.ToObject<int>() ?? BeginSessionResultState.UnknownError;
        var sessionToken = json?.Get("sessionToken")?.ToObject<SessionToken>();
        if (json == null || sessionToken == null || state != BeginSessionResultState.Success)
        {
            var why = json == null
                ? "empty answer"
                : state == BeginSessionResultState.UpdateRequired
                    ? "UpdateRequired: the access key is wrong, or the client version is older than the server expects (" + json.Get("expectedClientVersion") + ")"
                    : state.ToString();
            return (null, "BeginSession refused: " + why);
        }
        return (new BeginSessionResult { SessionToken = sessionToken, State = state, ExpectedClientVersion = json.Get("expectedClientVersion")?.ToString() }, null);
    }
}
