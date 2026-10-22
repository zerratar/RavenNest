using System.Diagnostics;
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
    private long lastV2Out, lastV2In, lastV2Saves, lastV2Resends;
    private DateTime lastAt = DateTime.UtcNow;
    private readonly CpuMeter? server;
    private readonly CpuMeter self = new(Process.GetCurrentProcess().Id);

    public Report(Metrics metrics, IReadOnlyList<StreamerSession> sessions, Options options)
    {
        m = metrics;
        this.sessions = sessions;
        o = options;
        if (o.ServerPid > 0)
        {
            server = new CpuMeter(o.ServerPid);
            if (!server.Valid)
            {
                Console.WriteLine("--server-pid " + o.ServerPid + ": no such process, not measuring the server");
                server = null;
            }
        }
    }

    /// <summary>
    /// CPU time and memory of a process on this machine (the server, or this tool), as cores busy on
    /// average: 1.0 is one core fully used.
    /// </summary>
    private sealed class CpuMeter
    {
        private readonly Process? process;
        private readonly TimeSpan startCpu;
        private readonly DateTime startAt = DateTime.UtcNow;
        private TimeSpan lastCpu;
        private DateTime lastAt = DateTime.UtcNow;

        public CpuMeter(int pid)
        {
            try
            {
                process = Process.GetProcessById(pid);
                startCpu = lastCpu = process.TotalProcessorTime;
            }
            catch (Exception exc) when (exc is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                process = null;
            }
        }

        public bool Valid => process != null;
        public long PeakWorkingSet { get; private set; }

        private (TimeSpan Cpu, long WorkingSet)? Read()
        {
            if (process == null) return null;
            try
            {
                process.Refresh();
                var ws = process.WorkingSet64;
                if (ws > PeakWorkingSet) PeakWorkingSet = ws;
                return (process.TotalProcessorTime, ws);
            }
            catch (InvalidOperationException)
            {
                return null; // it exited
            }
        }

        /// <summary>Cores busy since the last call, and the working set now.</summary>
        public (double Cores, long WorkingSet)? Window()
        {
            var r = Read();
            if (r == null) return null;
            var now = DateTime.UtcNow;
            var cores = (r.Value.Cpu - lastCpu).TotalSeconds / Math.Max(0.001, (now - lastAt).TotalSeconds);
            lastCpu = r.Value.Cpu;
            lastAt = now;
            return (cores, r.Value.WorkingSet);
        }

        /// <summary>CPU seconds and cores busy on average since the start.</summary>
        public (double CpuSeconds, double Cores)? Total()
        {
            var r = Read();
            if (r == null) return null;
            var cpu = (r.Value.Cpu - startCpu).TotalSeconds;
            return (cpu, cpu / Math.Max(0.001, (DateTime.UtcNow - startAt).TotalSeconds));
        }
    }

    private bool IsV2 => o.Protocol == WireProtocol.V2;

    private static string Fmt(double ms) => ms >= 1000 ? (ms / 1000).ToString("0.0") + "s" : ms.ToString("0") + "ms";

    private static string Bytes(long b) => b >= 1 << 20 ? (b / (double)(1 << 20)).ToString("0.0") + " MB" : (b / 1024.0).ToString("0.0") + " KB";

    public void PrintProgress()
    {
        var now = DateTime.UtcNow;
        var seconds = Math.Max(0.001, (now - lastAt).TotalSeconds);
        lastAt = now;

        var sb = new StringBuilder();
        sb.AppendLine();
        var links = IsV2
            ? $"  v2 up {sessions.Count(s => s.V2Connected)}/{sessions.Count}"
            : $"  delta up {sessions.Count(s => s.DeltaConnected)}/{sessions.Count}  events up {sessions.Count(s => s.EventsConnected)}/{sessions.Count}";
        sb.AppendLine($"--- {now - m.Started:hh\\:mm\\:ss}  players {sessions.Sum(s => s.JoinedCount)}/{sessions.Sum(s => s.Players.Length)}"
            + links
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
        if (IsV2)
        {
            var saves = Interlocked.Read(ref m.V2PlayerSaves);
            var resends = Interlocked.Read(ref m.V2Resends);
            var vout = Interlocked.Read(ref m.V2BytesOut);
            var vin = Interlocked.Read(ref m.V2BytesIn);
            var rt = m.V2SaveRoundTrip.TakeWindow();
            var stored = m.V2SaveStored.TakeWindow();
            var unconfirmed = sessions.Sum(s => s.V2Unconfirmed);
            m.V2UnconfirmedOverTime.Enqueue(((now - m.Started).TotalSeconds, unconfirmed));
            sb.AppendLine($"  v2 saves: +{saves - lastV2Saves} player saves (+{resends - lastV2Resends} resends), accepted p50 {Fmt(rt.P50)} p95 {Fmt(rt.P95)} p99 {Fmt(rt.P99)},"
                + $" stored p50 {Fmt(stored.P50)} p95 {Fmt(stored.P95)} p99 {Fmt(stored.P99)}; unconfirmed now {unconfirmed}");
            sb.AppendLine($"  v2 wire: {Bytes(vout - lastV2Out)} out, {Bytes(vin - lastV2In)} in; connects {m.V2Connects}, drops {m.V2Drops} (+{m.V2ChaosDrops} chaos), resumes {m.V2Resumes}, resume failures {m.V2ResumeFailures}");
            lastV2Saves = saves;
            lastV2Resends = resends;
            lastV2Out = vout;
            lastV2In = vin;
        }
        else
        {
            sb.AppendLine($"  delta: +{expFrames - lastExpFrames} exp frames, +{stateFrames - lastStateFrames} state frames, {Bytes(bytes - lastDeltaBytes)} sent"
                + $" (socket write p95 {Fmt(send.P95)}, max {Fmt(send.Max)}), send failures {Interlocked.Read(ref m.DeltaSendFailures)}");
        }
        var top = string.Join(", ", m.EventsByType.OrderByDescending(x => x.Value).Take(5).Select(x => x.Key + " " + x.Value));
        sb.AppendLine($"  events: +{events - lastEvents} received ({events} total{(top.Length > 0 ? ": " + top : "")})"
            + (IsV2 ? $"; gaps {m.V2EventGaps}, repeats {m.V2EventRepeats}, replayed after resume {m.V2EventReplays}" : ""));
        if (!IsV2)
        {
            sb.AppendLine($"  drops: delta {Interlocked.Read(ref m.DeltaDrops)}, events {Interlocked.Read(ref m.EventDrops)};"
                + $" join failures {Interlocked.Read(ref m.PlayersJoinFailed)}");
        }
        else
        {
            sb.AppendLine($"  join failures {Interlocked.Read(ref m.PlayersJoinFailed)}");
        }
        var cpu = server?.Window();
        var mine = self.Window();
        if (cpu != null || mine != null)
        {
            sb.AppendLine("  cpu (cores busy): " + (cpu != null ? $"server {cpu.Value.Cores:0.00}, {Bytes(cpu.Value.WorkingSet)} working set; " : "")
                + (mine != null ? $"load tester {mine.Value.Cores:0.00}" : ""));
        }
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
            + $", protocol {o.Protocol.ToString().ToLowerInvariant()}" + (o.V2Commands ? " with v2 commands" : "")
            + (o.V2DropEvery > TimeSpan.Zero ? $", v2 drop every {o.V2DropEvery.TotalSeconds:0} s" : "")
            + (o.V2RepeatShare > 0 ? $", repeat {o.V2RepeatShare:P0} of commands" : "")
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
        if (IsV2)
        {
            AppendV2(sb, elapsed);
        }
        else
        {
            sb.AppendLine($"  delta frames: {m.DeltaExpFrames} exp ({m.DeltaExpEntries} player entries, {m.DeltaSkillEntries} skill entries), "
                + $"{m.DeltaStateFrames} state ({m.DeltaStateEntries} entries), {m.DeltaGameStateFrames} game state, {m.DeltaAuthFrames} auth; {Bytes(m.DeltaBytes)}");
            sb.AppendLine($"    socket write p50 {Fmt(send.P50)} p95 {Fmt(send.P95)} p99 {Fmt(send.P99)} max {Fmt(send.Max)}; send failures {m.DeltaSendFailures}");
            sb.AppendLine($"  bytes on the wire (payload, without TCP/TLS): {Bytes(m.DeltaBytes + m.EventBytes)} ({Bytes((long)((m.DeltaBytes + m.EventBytes) / Math.Max(1, elapsed.TotalSeconds)))}/s)");
        }
        sb.AppendLine($"  events: {m.EventCount} in {m.EventBatches} messages ({Bytes(m.EventBytes)}), decode failures {m.EventDecodeFailures}"
            + (IsV2 ? $"; gaps {m.V2EventGaps}, repeats {m.V2EventRepeats}, replayed after resume {m.V2EventReplays}, acks sent {m.V2EventAcks}" : ""));
        foreach (var (type, count) in m.EventsByType.OrderByDescending(x => x.Value))
        {
            sb.AppendLine($"    {type,-28} {count}");
        }
        if (!IsV2)
        {
            sb.AppendLine($"  connections: delta {m.DeltaConnects} connects, {m.DeltaDrops} drops, {m.DeltaConnectFailures} failed connects;"
                + $" events {m.EventConnects} connects, {m.EventDrops} drops, {m.EventConnectFailures} failed connects, {m.EventChaosReconnects} chaos reconnects");
        }

        var cpu = server?.Total();
        var mine = self.Total();
        if (cpu != null)
        {
            sb.AppendLine($"  server cpu: {cpu.Value.CpuSeconds:0.0} s ({cpu.Value.Cores:0.00} cores busy on average), peak working set {Bytes(server!.PeakWorkingSet)}");
        }
        if (mine != null)
        {
            sb.AppendLine($"  load tester cpu: {mine.Value.CpuSeconds:0.0} s ({mine.Value.Cores:0.00} cores busy on average)");
        }

        AppendVerify(sb, rest);
        AppendVerify(sb, db);
        Console.Write(sb.ToString());
    }

    private void AppendV2(StringBuilder sb, TimeSpan elapsed)
    {
        var rt = m.V2SaveRoundTrip.Total();
        var stored = m.V2SaveStored.Total();
        var unconfirmed = m.V2Unconfirmed.Total();
        var seconds = Math.Max(1, elapsed.TotalSeconds);
        sb.AppendLine($"  v2 saves: {m.V2SaveBatches} batches, {m.V2PlayerSaves} player saves ({m.V2SkillEntries} skill entries, {m.V2FullSaves} full), {m.V2Resends} resends");
        sb.AppendLine($"    send to Accepted (per batch) p50 {Fmt(rt.P50)} p95 {Fmt(rt.P95)} p99 {Fmt(rt.P99)} max {Fmt(rt.Max)}; {m.V2BatchesWithoutAck} batches never acknowledged");
        sb.AppendLine($"    send to Stored (per player version) p50 {Fmt(stored.P50)} p95 {Fmt(stored.P95)} p99 {Fmt(stored.P99)} max {Fmt(stored.Max)}");
        sb.AppendLine($"    acks: {m.V2AcceptedAcks} Accepted answers (one per batch), {m.V2StoredAcks} Stored acks (per player); players with unstored saves per session per tick: p50 {unconfirmed.P50:0} p95 {unconfirmed.P95:0} max {unconfirmed.Max:0}");
        var statuses = string.Join(", ", m.V2Counts.Where(x => x.Key.StartsWith("save ")).OrderByDescending(x => x.Value).Select(x => x.Key[5..] + " " + x.Value));
        sb.AppendLine("    refused saves: " + (statuses.Length > 0 ? statuses : "none"));
        if (m.V2DrainSeconds != null)
        {
            sb.AppendLine(m.V2LeftUnstored == 0
                ? $"    at the end every save was stored within {m.V2DrainSeconds:0.0} s"
                : $"    at the end {m.V2LeftUnstored} players still had unstored saves after {m.V2DrainSeconds:0.0} s");
        }
        sb.AppendLine($"  v2 wire (payload, without WebSocket/TLS framing): {Bytes(m.V2BytesOut)} out ({Bytes((long)(m.V2BytesOut / seconds))}/s), {Bytes(m.V2BytesIn)} in ({Bytes((long)(m.V2BytesIn / seconds))}/s);"
            + $" {m.V2MessagesOut} messages out, {m.V2MessagesIn} in, decode failures {m.V2DecodeFailures}");
        sb.AppendLine($"  v2 connection: {m.V2Connects} connects, {m.V2ConnectFailures} failed, {m.V2Drops} drops by the server or network, {m.V2ChaosDrops} chaos drops,"
            + $" {m.V2Resumes} resumed, {m.V2ResumeFailures} not resumed, {m.V2Resyncs} resyncs");
        sb.AppendLine($"  v2 inventory: {m.V2ItemAddEvents} ItemAdd events, {m.V2ItemAddWithInventory} with an inventory update; {m.V2RevisionUpdates} revisions seen (events and command results),"
            + $" {m.V2RevisionGaps} gaps ({m.V2RevisionMissing} revisions missing), {m.V2RevisionOutOfOrder} out of order, {m.V2RevisionRestarts} numbering restarts (server restarted)");
        if (o.V2Commands)
        {
            sb.AppendLine($"  v2 commands: {m.V2CommandResends} resent after a reconnect, {m.V2DuplicateResults} duplicate answers, {m.V2LateResults} answers nobody waited for");
            var cs = string.Join(", ", m.V2Counts.Where(x => x.Key.StartsWith("command ")).OrderBy(x => x.Key).Select(x => x.Key[8..] + " " + x.Value));
            if (cs.Length > 0) sb.AppendLine("    statuses: " + cs);
            if (o.V2RepeatShare > 0)
            {
                sb.AppendLine($"    repeats with the same request id: {m.V2CommandRepeats}, same answer {m.V2RepeatMatches}, different {m.V2RepeatMismatches};"
                    + $" items.add checked against the server {m.V2RepeatChecked}, added twice {m.V2DoubleAdds}");
            }
        }
        var other = m.V2Counts.Where(x => !x.Key.StartsWith("save ") && !x.Key.StartsWith("command ")).OrderByDescending(x => x.Value).ToList();
        foreach (var (what, count) in other.Take(10))
        {
            sb.AppendLine($"    {what}: {count}");
        }
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
                o.ClientVersion,
                protocol = o.Protocol.ToString().ToLowerInvariant(),
                o.V2Commands,
                o.V2RepeatShare,
                v2DropEverySeconds = o.V2DropEvery.TotalSeconds,
                v2ResendAfterSeconds = o.V2ResendAfter.TotalSeconds
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
            v2 = IsV2 ? new
            {
                saves = new
                {
                    batches = m.V2SaveBatches,
                    playerSaves = m.V2PlayerSaves,
                    skillEntries = m.V2SkillEntries,
                    fullSaves = m.V2FullSaves,
                    resends = m.V2Resends,
                    batchesWithoutAck = m.V2BatchesWithoutAck,
                    acceptedAcks = m.V2AcceptedAcks,
                    storedAcks = m.V2StoredAcks,
                    roundTripToAccepted = m.V2SaveRoundTrip.Total(),
                    sendToStored = m.V2SaveStored.Total(),
                    unconfirmedPerSessionPerTick = m.V2Unconfirmed.Total(),
                    unconfirmedOverTime = m.V2UnconfirmedOverTime.Select(x => new { seconds = Math.Round(x.Seconds), players = x.Players }),
                    drainSeconds = m.V2DrainSeconds,
                    leftUnstored = m.V2LeftUnstored
                },
                wire = new { bytesOut = m.V2BytesOut, bytesIn = m.V2BytesIn, messagesOut = m.V2MessagesOut, messagesIn = m.V2MessagesIn, decodeFailures = m.V2DecodeFailures },
                connection = new { connects = m.V2Connects, connectFailures = m.V2ConnectFailures, drops = m.V2Drops, chaosDrops = m.V2ChaosDrops, resumes = m.V2Resumes, resumeFailures = m.V2ResumeFailures, resyncs = m.V2Resyncs },
                events = new { gaps = m.V2EventGaps, repeats = m.V2EventRepeats, replayedAfterResume = m.V2EventReplays, acks = m.V2EventAcks },
                inventory = new { itemAddEvents = m.V2ItemAddEvents, itemAddWithInventory = m.V2ItemAddWithInventory, revisions = m.V2RevisionUpdates, gaps = m.V2RevisionGaps, missing = m.V2RevisionMissing, outOfOrder = m.V2RevisionOutOfOrder, restarts = m.V2RevisionRestarts },
                commands = new
                {
                    resentAfterReconnect = m.V2CommandResends,
                    duplicateAnswers = m.V2DuplicateResults,
                    answersNobodyWaitedFor = m.V2LateResults,
                    repeats = m.V2CommandRepeats,
                    repeatSame = m.V2RepeatMatches,
                    repeatDifferent = m.V2RepeatMismatches,
                    repeatAddItemChecked = m.V2RepeatChecked,
                    repeatAddedTwice = m.V2DoubleAdds
                },
                counts = m.V2Counts.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value)
            } : null,
            bytesOnWire = IsV2 ? m.V2BytesOut + m.V2BytesIn : m.DeltaBytes + m.EventBytes,
            serverCpu = server?.Total() is { } sc ? new { cpuSeconds = sc.CpuSeconds, coresBusy = sc.Cores, peakWorkingSet = server.PeakWorkingSet } : null,
            loadTesterCpu = self.Total() is { } mc ? new { cpuSeconds = mc.CpuSeconds, coresBusy = mc.Cores } : null,
            verifyRest = rest,
            verifyDb = db
        };
        File.WriteAllText(path, JsonConvert.SerializeObject(data, Formatting.Indented));
        Console.WriteLine("wrote " + Path.GetFullPath(path));
    }
}
