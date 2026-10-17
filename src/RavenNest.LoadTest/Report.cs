using System.Text;
using Newtonsoft.Json;
using RavenNest.LoadTest.Simulation;

namespace RavenNest.LoadTest;

public sealed class Report
{
    private readonly Metrics m;
    private readonly IReadOnlyList<StreamerSession> sessions;
    private readonly Options o;
    private long lastExpFrames, lastStateFrames, lastEvents, lastDeltaBytes;
    private DateTime lastAt = DateTime.UtcNow;

    public Report(Metrics metrics, IReadOnlyList<StreamerSession> sessions, Options options)
    {
        m = metrics;
        this.sessions = sessions;
        o = options;
    }

    private static string Fmt(double ms) => ms >= 1000 ? (ms / 1000).ToString("0.0") + "s" : ms.ToString("0") + "ms";

    private static string Bytes(long b) => b >= 1 << 20 ? (b / (double)(1 << 20)).ToString("0.0") + " MB" : (b / 1024.0).ToString("0.0") + " KB";

    public void PrintProgress()
    {
        var now = DateTime.UtcNow;
        var seconds = Math.Max(0.001, (now - lastAt).TotalSeconds);
        lastAt = now;

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine($"--- {now - m.Started:hh\\:mm\\:ss}  players {sessions.Sum(s => s.JoinedCount)}/{sessions.Sum(s => s.Players.Length)}"
            + $"  delta up {sessions.Count(s => s.DeltaConnected)}/{sessions.Count}  events up {sessions.Count(s => s.EventsConnected)}/{sessions.Count}"
            + $"  tick overruns {sessions.Sum(s => s.TickOverruns)}, worst tick {Fmt(sessions.Max(s => s.WorstTickMs))} ---");

        var rows = m.Endpoints.OrderBy(x => x.Key).Select(x => (x.Key, S: x.Value.TakeWindow())).Where(x => x.S.Count > 0).ToList();
        if (rows.Count > 0)
        {
            sb.AppendLine($"  {"last " + seconds.ToString("0") + " s",-38} {"req/s",7} {"ok",7} {"fail",6} {"p50",7} {"p95",7} {"p99",7}");
            foreach (var (name, s) in rows)
            {
                sb.AppendLine($"  {name,-38} {s.Count / seconds,7:0.0} {s.Ok,7} {s.Failed,6} {Fmt(s.P50),7} {Fmt(s.P95),7} {Fmt(s.P99),7}");
            }
        }

        var expFrames = Interlocked.Read(ref m.DeltaExpFrames);
        var stateFrames = Interlocked.Read(ref m.DeltaStateFrames);
        var events = Interlocked.Read(ref m.EventCount);
        var bytes = Interlocked.Read(ref m.DeltaBytes);
        var send = m.DeltaSend.TakeWindow();
        sb.AppendLine($"  delta: +{expFrames - lastExpFrames} exp frames, +{stateFrames - lastStateFrames} state frames, {Bytes(bytes - lastDeltaBytes)} sent"
            + $" (socket write p95 {Fmt(send.P95)}, max {Fmt(send.Max)}), send failures {Interlocked.Read(ref m.DeltaSendFailures)}");
        var top = string.Join(", ", m.EventsByType.OrderByDescending(x => x.Value).Take(5).Select(x => x.Key + " " + x.Value));
        sb.AppendLine($"  events: +{events - lastEvents} received ({events} total{(top.Length > 0 ? ": " + top : "")})");
        sb.AppendLine($"  drops: delta {Interlocked.Read(ref m.DeltaDrops)}, events {Interlocked.Read(ref m.EventDrops)};"
            + $" join failures {Interlocked.Read(ref m.PlayersJoinFailed)}");
        lastExpFrames = expFrames;
        lastStateFrames = stateFrames;
        lastEvents = events;
        lastDeltaBytes = bytes;
        Console.Write(sb.ToString());
    }

    public void PrintFinal(VerifyResult? rest, VerifyResult? db)
    {
        var elapsed = DateTime.UtcNow - m.Started;
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("================ RESULT ================");
        sb.AppendLine($"ran {elapsed:hh\\:mm\\:ss}, {sessions.Count} session(s), {o.Players} players each, tick {o.Tick.TotalSeconds:0.##} s, client version {o.ClientVersion}"
            + (o.NewConnectionPerRequest ? ", new HTTP connection per request" : ""));

        sb.AppendLine();
        sb.AppendLine($"  {"endpoint",-38} {"req/s",7} {"ok",7} {"fail",6} {"p50",7} {"p95",7} {"p99",7} {"max",7}");
        foreach (var (name, stats) in m.Endpoints.OrderBy(x => x.Key))
        {
            var s = stats.Total();
            sb.AppendLine($"  {name,-38} {s.Count / Math.Max(1, elapsed.TotalSeconds),7:0.0} {s.Ok,7} {s.Failed,6} {Fmt(s.P50),7} {Fmt(s.P95),7} {Fmt(s.P99),7} {Fmt(s.Max),7}");
        }

        var anyFailures = false;
        foreach (var (name, stats) in m.Endpoints.OrderBy(x => x.Key))
        {
            var failures = stats.Failures();
            if (failures.Count == 0) continue;
            if (!anyFailures) { sb.AppendLine(); sb.AppendLine("  failures:"); anyFailures = true; }
            foreach (var (message, count) in failures.Take(5))
            {
                sb.AppendLine($"    {name}: {count} x {message}");
            }
        }

        var send = m.DeltaSend.Total();
        sb.AppendLine();
        sb.AppendLine($"  players: {m.PlayersJoined} joined ({m.PlayersNew} new users, {m.PlayersRestored} restored), {m.PlayersJoinFailed} failed");
        foreach (var s in sessions.Where(s => s.JoinStarted != null && s.JoinFinished != null))
        {
            sb.AppendLine($"    {s.Name}: {s.JoinedCount}/{s.Players.Length} in {(s.JoinFinished - s.JoinStarted)!.Value.TotalSeconds:0.0} s, {s.Ticks} ticks, {s.TickOverruns} over {o.Tick.TotalSeconds:0.##} s, worst {Fmt(s.WorstTickMs)}");
        }
        sb.AppendLine($"  delta frames: {m.DeltaExpFrames} exp ({m.DeltaExpEntries} player entries, {m.DeltaSkillEntries} skill entries), "
            + $"{m.DeltaStateFrames} state ({m.DeltaStateEntries} entries), {m.DeltaGameStateFrames} game state, {m.DeltaAuthFrames} auth; {Bytes(m.DeltaBytes)}");
        sb.AppendLine($"    socket write p50 {Fmt(send.P50)} p95 {Fmt(send.P95)} p99 {Fmt(send.P99)} max {Fmt(send.Max)}; send failures {m.DeltaSendFailures}");
        sb.AppendLine($"  events: {m.EventCount} in {m.EventBatches} messages ({Bytes(m.EventBytes)}), decode failures {m.EventDecodeFailures}");
        foreach (var (type, count) in m.EventsByType.OrderByDescending(x => x.Value))
        {
            sb.AppendLine($"    {type,-28} {count}");
        }
        sb.AppendLine($"  connections: delta {m.DeltaConnects} connects, {m.DeltaDrops} drops, {m.DeltaConnectFailures} failed connects;"
            + $" events {m.EventConnects} connects, {m.EventDrops} drops, {m.EventConnectFailures} failed connects");

        AppendVerify(sb, rest);
        AppendVerify(sb, db);
        Console.Write(sb.ToString());
    }

    private static void AppendVerify(StringBuilder sb, VerifyResult? v)
    {
        if (v == null) return;
        sb.AppendLine();
        sb.AppendLine("  verification via " + v.Source + ":");
        if (v.Note != null) sb.AppendLine("    " + v.Note);
        sb.AppendLine($"    {v.Checked} players checked, {v.ReadFailures} could not be read");
        foreach (var msg in v.ReadFailureMessages) sb.AppendLine("      " + msg);
        if (v.Checked > 0 && v.ReadFailures == 0 && v.BehindAtFirstCheck.Count == 0)
        {
            sb.AppendLine("    every checked player has at least the exp that was sent: no lost saves");
        }
        else if (v.BehindAtFirstCheck.Count > 0)
        {
            sb.AppendLine($"    behind at the first check: {v.BehindAtFirstCheck.Count}; still behind at the end: {v.StillBehind.Count}");
            foreach (var p in (v.StillBehind.Count > 0 ? v.StillBehind : v.BehindAtFirstCheck).Take(10))
            {
                var g = string.Join("; ", p.Gaps.Take(3).Select(x => $"{x.Skill} sent L{x.SentLevel} {x.SentExp} xp, saved L{x.SavedLevel} {x.SavedExp:0} xp"));
                sb.AppendLine($"      {p.Streamer} {p.UserName} ({p.CharacterId}): {g}");
            }
        }
        if (v.CaughtUpAfterSeconds != null)
        {
            sb.AppendLine($"    everything was there after {v.CaughtUpAfterSeconds:0.0} s");
        }
        if (v.LostSaves)
        {
            sb.AppendLine($"    LOST SAVES: {v.StillBehind.Count} players are behind what was sent");
        }
    }

    public void WriteJson(string path, VerifyResult? rest, VerifyResult? db)
    {
        var data = new
        {
            started = m.Started,
            finished = DateTime.UtcNow,
            options = new
            {
                url = o.BaseUrl.ToString(),
                o.Players,
                o.Sessions,
                durationSeconds = o.Duration.TotalSeconds,
                tickSeconds = o.Tick.TotalSeconds,
                joinMode = o.JoinMode.ToString(),
                o.CommandRate,
                o.Commands,
                o.ExpShare,
                o.StateShare,
                o.NewConnectionPerRequest,
                o.ClientVersion
            },
            endpoints = m.Endpoints.OrderBy(x => x.Key).ToDictionary(x => x.Key, x =>
            {
                var s = x.Value.Total();
                return (object)new { s.Ok, s.Failed, s.P50, s.P95, s.P99, s.Max, s.Mean, failures = x.Value.Failures().Take(10).ToDictionary(f => f.Key, f => f.Value) };
            }),
            delta = new
            {
                m.DeltaExpFrames, m.DeltaStateFrames, m.DeltaGameStateFrames, m.DeltaAuthFrames,
                m.DeltaExpEntries, m.DeltaSkillEntries, m.DeltaStateEntries, m.DeltaBytes, m.DeltaSendFailures,
                m.DeltaConnects, m.DeltaDrops, m.DeltaConnectFailures,
                socketWrite = m.DeltaSend.Total()
            },
            events = new { m.EventCount, m.EventBatches, m.EventBytes, m.EventDecodeFailures, m.EventConnects, m.EventDrops, m.EventConnectFailures, byType = m.EventsByType.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value) },
            players = new { m.PlayersJoined, m.PlayersNew, m.PlayersRestored, m.PlayersJoinFailed },
            sessions = sessions.Select(s => new
            {
                s.Name,
                s.JoinedCount,
                joinSeconds = s.JoinStarted != null && s.JoinFinished != null ? (s.JoinFinished - s.JoinStarted)!.Value.TotalSeconds : (double?)null,
                s.Ticks,
                s.TickOverruns,
                s.WorstTickMs
            }),
            verifyRest = rest,
            verifyDb = db
        };
        File.WriteAllText(path, JsonConvert.SerializeObject(data, Formatting.Indented));
        Console.WriteLine("wrote " + Path.GetFullPath(path));
    }
}
