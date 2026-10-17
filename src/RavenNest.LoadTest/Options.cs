using System.Globalization;

namespace RavenNest.LoadTest;

public enum JoinMode
{
    /// <summary>One POST api/players per player, the way a chat !join reaches the server.</summary>
    Join,

    /// <summary>
    /// One POST api/players/restore with every known character, the way the game restores its
    /// players after a restart. Players without a known character id are joined one by one first.
    /// </summary>
    Restore
}

public enum WireProtocol
{
    /// <summary>What the game does today: the delta socket (3921), the event socket (3920) and REST commands.</summary>
    V1,

    /// <summary>The v2 game connection: one WebSocket at /api/v2/game for saves, events and (optionally) commands.</summary>
    V2
}

public sealed class Options
{
    public const string PasswordEnv = "RAVENNEST_LOADTEST_PASSWORD";
    public const string AccessKeyEnv = "RAVENNEST_ACCESS_KEY";
    public const string AuthTokenEnv = "RAVENNEST_LOADTEST_AUTH_TOKEN";
    public const string DbEnv = "RAVENNEST_LOADTEST_DB";

    public string Command { get; set; } = "run";

    // ---- where ----
    public Uri BaseUrl { get; set; } = new Uri("https://127.0.0.1");
    public string? TcpHost { get; set; }
    public int EventPort { get; set; } = 3920;
    public int DeltaPort { get; set; } = 3921;
    public bool AllowNonLocal { get; set; }

    // ---- who ----
    public string StreamerName { get; set; } = "loadtest_streamer";
    public string? Password { get; set; }
    public string? AccessKey { get; set; }
    public string? AuthToken { get; set; }
    public string ClientVersion { get; set; } = "0.9.4.4a";
    public string PlayerPrefix { get; set; } = "loadtest";

    // ---- how much ----
    public int Players { get; set; } = 1000;
    public int Sessions { get; set; } = 1;
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan Tick { get; set; } = TimeSpan.FromSeconds(2);
    public JoinMode JoinMode { get; set; } = JoinMode.Join;
    public int JoinConcurrency { get; set; } = 10;
    public int JoinDelayMs { get; set; } = 200;
    public double ExpShare { get; set; } = 0.9;
    public double StateShare { get; set; } = 0.8;
    public double ExpRate { get; set; } = 25;
    public int MaxBatch { get; set; } = 2048;
    public double CommandRate { get; set; }
    public string[] Commands { get; set; } = new[] { "additem", "produce", "equipall", "fetch" };
    public bool NewConnectionPerRequest { get; set; }

    /// <summary>Every this often, opens a second event socket and then closes the first, as the game does when it reconnects. Zero is off.</summary>
    public TimeSpan EventReconnectEvery { get; set; }
    public int RequestTimeoutSeconds { get; set; } = 30;

    // ---- v2 ----
    public WireProtocol Protocol { get; set; } = WireProtocol.V1;

    /// <summary>Joins, restores and commands go as v2 CommandRequests instead of REST (fetch stays on REST).</summary>
    public bool V2Commands { get; set; }

    /// <summary>Share of v2 commands sent a second time with the same request id, to check the answer is the first one.</summary>
    public double V2RepeatShare { get; set; }

    /// <summary>Every this often, drop the WebSocket without a close and resume. Zero is off.</summary>
    public TimeSpan V2DropEvery { get; set; }

    /// <summary>At the end, how long to keep resending until every save is stored (the game's quit wait).</summary>
    public TimeSpan V2DrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>The server's process id, to report its CPU time and memory. Zero is off.</summary>
    public int ServerPid { get; set; }

    // ---- checking ----
    public bool Verify { get; set; } = true;
    public int VerifySample { get; set; } = 200;
    public TimeSpan VerifyDelay { get; set; } = TimeSpan.FromSeconds(3);
    public TimeSpan VerifyRecheck { get; set; } = TimeSpan.FromSeconds(10);
    public bool VerifyDb { get; set; }
    public TimeSpan VerifyDbTimeout { get; set; } = TimeSpan.FromSeconds(60);
    public bool EndSession { get; set; } = true;

    // ---- output ----
    public TimeSpan ReportInterval { get; set; } = TimeSpan.FromSeconds(10);
    public string? JsonReport { get; set; }
    public string RosterFolder { get; set; } = "loadtest-rosters";

    // ---- seed ----
    public bool Execute { get; set; }
    public string SeedOut { get; set; } = "loadtest-seed.sql";

    public string EffectiveTcpHost => TcpHost ?? BaseUrl.Host;

    public string StreamerNameFor(int sessionIndex) => StreamerName + (sessionIndex + 1);

    public static Options Parse(string[] args)
    {
        var o = new Options();
        var i = 0;
        if (args.Length > 0 && !args[0].StartsWith("-"))
        {
            o.Command = args[0].ToLowerInvariant();
            i = 1;
        }

        for (; i < args.Length; ++i)
        {
            var a = args[i];
            string Next()
            {
                if (i + 1 >= args.Length) throw new ArgumentException("Missing value for " + a);
                return args[++i];
            }

            switch (a.ToLowerInvariant())
            {
                case "-h":
                case "--help": o.Command = "help"; break;
                case "--url":
                case "--base-url": o.BaseUrl = new Uri(Next().TrimEnd('/')); break;
                case "--tcp-host": o.TcpHost = Next(); break;
                case "--event-port": o.EventPort = Int(Next()); break;
                case "--delta-port": o.DeltaPort = Int(Next()); break;
                case "--i-know-this-is-not-production": o.AllowNonLocal = true; break;

                case "--streamer": o.StreamerName = Next(); break;
                case "--password": o.Password = Next(); break;
                case "--access-key": o.AccessKey = Next(); break;
                case "--auth-token": o.AuthToken = Next(); break;
                case "--client-version": o.ClientVersion = Next(); break;
                case "--player-prefix": o.PlayerPrefix = Next(); break;

                case "--players": o.Players = Int(Next()); break;
                case "--sessions": o.Sessions = Int(Next()); break;
                case "--duration": o.Duration = Span(Next()); break;
                case "--tick": o.Tick = Span(Next()); break;
                case "--join-mode": o.JoinMode = Enum.Parse<JoinMode>(Next(), true); break;
                case "--join-concurrency": o.JoinConcurrency = Int(Next()); break;
                case "--join-delay-ms": o.JoinDelayMs = Int(Next()); break;
                case "--exp-share": o.ExpShare = Dbl(Next()); break;
                case "--state-share": o.StateShare = Dbl(Next()); break;
                case "--exp-rate": o.ExpRate = Dbl(Next()); break;
                case "--max-batch": o.MaxBatch = Int(Next()); break;
                case "--command-rate": o.CommandRate = Dbl(Next()); break;
                case "--commands": o.Commands = Next().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.ToLowerInvariant()).ToArray(); break;
                case "--new-connection-per-request": o.NewConnectionPerRequest = true; break;
                case "--event-reconnect-every": o.EventReconnectEvery = Span(Next()); break;
                case "--request-timeout": o.RequestTimeoutSeconds = Int(Next()); break;

                case "--protocol": o.Protocol = Enum.Parse<WireProtocol>(Next(), true); break;
                case "--v2-commands": o.V2Commands = true; break;
                case "--v2-repeat-share": o.V2RepeatShare = Dbl(Next()); break;
                case "--v2-drop-every": o.V2DropEvery = Span(Next()); break;
                case "--v2-drain-timeout": o.V2DrainTimeout = Span(Next()); break;
                case "--server-pid": o.ServerPid = Int(Next()); break;

                case "--verify": o.Verify = Bool(Next()); break;
                case "--no-verify": o.Verify = false; break;
                case "--verify-sample": o.VerifySample = Int(Next()); break;
                case "--verify-delay": o.VerifyDelay = Span(Next()); break;
                case "--verify-recheck": o.VerifyRecheck = Span(Next()); break;
                case "--verify-db": o.VerifyDb = true; break;
                case "--verify-db-timeout": o.VerifyDbTimeout = Span(Next()); break;
                case "--keep-session": o.EndSession = false; break;

                case "--report-interval": o.ReportInterval = Span(Next()); break;
                case "--json-report": o.JsonReport = Next(); break;
                case "--roster-folder": o.RosterFolder = Next(); break;

                case "--execute": o.Execute = true; break;
                case "--out": o.SeedOut = Next(); break;

                default: throw new ArgumentException("Unknown option: " + a + " (see --help)");
            }
        }

        o.Password ??= Environment.GetEnvironmentVariable(PasswordEnv);
        o.AccessKey ??= Environment.GetEnvironmentVariable(AccessKeyEnv);
        o.AuthToken ??= Environment.GetEnvironmentVariable(AuthTokenEnv);

        if (o.Players < 0) throw new ArgumentException("--players must be 0 or more");
        if (o.Sessions < 1) throw new ArgumentException("--sessions must be 1 or more");
        if (o.Tick <= TimeSpan.Zero) throw new ArgumentException("--tick must be above zero");
        if (o.JoinConcurrency < 1) o.JoinConcurrency = 1;
        if (o.MaxBatch < 1) o.MaxBatch = 1;
        if (o.V2RepeatShare < 0 || o.V2RepeatShare > 1) throw new ArgumentException("--v2-repeat-share must be between 0 and 1");
        if (o.Protocol != WireProtocol.V2 && (o.V2Commands || o.V2RepeatShare > 0 || o.V2DropEvery > TimeSpan.Zero))
        {
            throw new ArgumentException("--v2-commands, --v2-repeat-share and --v2-drop-every need --protocol v2");
        }
        if (o.V2RepeatShare > 0 && !o.V2Commands)
        {
            throw new ArgumentException("--v2-repeat-share repeats v2 commands: add --v2-commands");
        }
        if (o.ClientVersion.Contains("9.4.0"))
        {
            // DeltaServer reads player state in an older layout for any version containing "9.4.0".
            throw new ArgumentException("--client-version must not contain \"9.4.0\": the server would parse the state frames with the old 9.4.0 layout.");
        }
        return o;
    }

    private static int Int(string v) => int.Parse(v, CultureInfo.InvariantCulture);
    private static double Dbl(string v) => double.Parse(v, CultureInfo.InvariantCulture);
    private static bool Bool(string v) => v is "1" or "on" or "yes" or "true" or "True";

    /// <summary>Plain seconds ("90"), or with a unit: 500ms, 30s, 5m, 1h.</summary>
    public static TimeSpan Span(string v)
    {
        v = v.Trim().ToLowerInvariant();
        if (v.EndsWith("ms")) return TimeSpan.FromMilliseconds(Dbl(v[..^2]));
        if (v.EndsWith("s")) return TimeSpan.FromSeconds(Dbl(v[..^1]));
        if (v.EndsWith("m")) return TimeSpan.FromMinutes(Dbl(v[..^1]));
        if (v.EndsWith("h")) return TimeSpan.FromHours(Dbl(v[..^1]));
        return TimeSpan.FromSeconds(Dbl(v));
    }

    public const string Help = """
RavenNest load tester. Simulates streamers running Ravenfall against a LOCAL RavenNest server.

Usage:
  RavenNest.LoadTest [run] [options]     run a load test (default)
  RavenNest.LoadTest seed [options]      write the SQL that creates the test streamer account(s)
  RavenNest.LoadTest self-test           check the delta and TCP encoding against the server's own parsers (no network)
  RavenNest.LoadTest help

Where:
  --url <url>                 server base url (default https://127.0.0.1, what the game's local server mode uses)
  --tcp-host <host>           host for the TCP sockets (default: the url's host)
  --event-port <n>            game event socket (default 3920)
  --delta-port <n>            delta socket (default 3921)
  --i-know-this-is-not-production   allow a host that is not localhost/127.0.0.1/::1

Who (secrets come from environment variables; the command line is visible to other processes):
  --streamer <name>           test streamer account (default loadtest_streamer; with --sessions N: loadtest_streamer1..N)
  --password <pw>             or env RAVENNEST_LOADTEST_PASSWORD
  --access-key <key>          the game's access key, or env RAVENNEST_ACCESS_KEY
  --auth-token <base64>       an auth-token header value instead of a password, or env RAVENNEST_LOADTEST_AUTH_TOKEN (one session only)
  --client-version <v>        version sent to BeginSession (default 0.9.4.4a)
  --player-prefix <p>         simulated players are <p>_s<session>_<n> (default loadtest); reuse it to reuse the same players

How much:
  --players <n>               players per session (default 1000)
  --sessions <n>              concurrent streamer sessions (default 1)
  --duration <t>              how long to run after the session started (default 5m; units ms/s/m/h)
  --tick <t>                  delta interval (default 2s, the game's GameSyncInterval)
  --join-mode join|restore    join each player (chat !join) or one restore request (game restart) (default join)
  --join-concurrency <n>      joins in flight at once (default 10)
  --join-delay-ms <n>         pause between join batches (default 200)
  --exp-share <0..1>          share of players gaining exp each tick (default 0.9)
  --state-share <0..1>        share of players whose state changes each tick (default 0.8)
  --exp-rate <n>              base exp per tick, scaled by level (default 25)
  --max-batch <n>             players per delta frame (default 2048, as the game)
  --command-rate <n>          REST commands per second per session (default 0 = off)
  --commands <list>           any of additem,produce,craft,equipall,fetch,highscore (default additem,produce,equipall,fetch)
  --new-connection-per-request   open a new HTTP connection for every request, as the game client currently does
  --event-reconnect-every <span> every this often open a second event socket, then close the first (the game's reconnect)
  --request-timeout <s>       per request timeout in seconds (default 30)

Protocol:
  --protocol v1|v2            v1 (default): delta socket 3921 + event socket 3920, what the game does today.
                              v2: one WebSocket at /api/v2/game for saves (with Accepted/Stored acks) and events
  --v2-commands               v2 only: joins, restores and the command mix go as v2 CommandRequests (fetch stays on REST)
  --v2-repeat-share <0..1>    v2 only: resend this share of commands with the same request id and check the answer
                              is the first one and nothing was added twice (needs --v2-commands)
  --v2-drop-every <span>      v2 only: drop the WebSocket without a close this often and resume with the same resume id
  --v2-drain-timeout <t>      v2 only: at the end, how long to keep resending until every save is stored (default 30s)
  --server-pid <pid>          report the server process's CPU time and memory (Get-NetTCPConnection -LocalPort 443)

Checking:
  --no-verify                 skip the read back at the end
  --verify-sample <n>         players per session to read back (default 200, 0 = all)
  --verify-delay <t>          wait before reading back (default 3s)
  --verify-recheck <t>        wait before re-reading players that were behind (default 10s)
  --verify-db                 also read the saved skills straight from the database (read only), connection string in env RAVENNEST_LOADTEST_DB
  --verify-db-timeout <t>     how long to wait for the server to write them (default 60s)
  --keep-session              do not end the game session at the end

Output:
  --report-interval <t>       progress report interval (default 10s)
  --json-report <file>        write the final numbers as JSON, for comparing runs
  --roster-folder <dir>       where character ids are remembered between runs (default loadtest-rosters)

Seed:
  --sessions <n> --streamer <name> --password <pw>   which accounts to create
  --out <file>                SQL file to write (default loadtest-seed.sql)
  --execute                   also run it against env RAVENNEST_LOADTEST_DB (stop the server first, start it after)
""";
}
