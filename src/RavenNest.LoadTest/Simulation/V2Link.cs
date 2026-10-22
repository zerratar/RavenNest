using System.Collections.Concurrent;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using RavenNest.LoadTest.V2;
using RavenNest.Models;
using RavenNest.Protocol;

namespace RavenNest.LoadTest.Simulation;

/// <summary>
/// The game side of the v2 connection for one streamer session, as the game client is meant to do it
/// (docs/network-v2.md in the game repo): saves from a per player outbox until the server says they are
/// stored, events applied in sequence and acknowledged, commands by request id, and resuming after a drop
/// with the same resume id and the last applied event. Replaces the delta socket and the event socket.
/// </summary>
public sealed class V2Link : IAsyncDisposable
{
    private static readonly uint AllSkills = SimPlayer.SkillCount >= 32 ? uint.MaxValue : (1u << SimPlayer.SkillCount) - 1;

    private readonly Options o;
    private readonly Metrics metrics;
    private readonly Action<string> log;
    private readonly string connectionToken;
    private readonly Func<int, GameStateSave> gameState;
    private readonly Uri endpoint;
    private readonly HttpMessageInvoker invoker = V2Connection.CreateInvoker();
    private readonly Guid resumeId = Guid.NewGuid();
    private readonly SemaphoreSlim connectLock = new(1, 1);
    private readonly ConcurrentDictionary<Guid, PendingCommand> commands = new();
    private readonly Dictionary<Guid, long> revisions = new(); // newest inventory revision seen per character, under gate

    // everything below is guarded by gate: the tick builds saves, the read loop applies acks and events
    private readonly object gate = new();
    private readonly Dictionary<Guid, SimPlayer> byCharacter = new();
    private readonly Queue<SentBatch> unacked = new();
    private long batchSequence; // keeps counting across reconnects, as the game's NetworkV2
    private V2Connection? connection;
    private bool everWelcomed;
    private long lastApplied, lastAcked, replayUntil;
    private int lastGameStatePlayerCount = -1;
    private long eventGapsLogged, mismatchesLogged;

    private DateTime nextConnect = DateTime.MinValue;
    private DateTime downUntil = DateTime.MinValue; // a chaos drop's outage: the tick does not reconnect before
    private TimeSpan backoff = TimeSpan.FromSeconds(1);
    private bool giveUp;

    public V2Link(Options options, Metrics metrics, Action<string> log, string connectionToken, Func<int, GameStateSave> gameState)
    {
        o = options;
        this.metrics = metrics;
        this.log = log;
        this.connectionToken = connectionToken;
        this.gameState = gameState;
        endpoint = V2Connection.EndpointFor(options.BaseUrl);
    }

    public bool Connected => connection?.IsOpen ?? false;

    /// <summary>Players whose newest version is not stored yet, as of the last tick.</summary>
    public int Unconfirmed { get; private set; }

    public long LastAppliedEvent
    {
        get { lock (gate) return lastApplied; }
    }

    private sealed class SentBatch
    {
        public long Sequence;
        public long SentAt;
    }

    private sealed class PendingCommand
    {
        public required CommandRequest Request;
        public bool IsRepeat;
        public readonly TaskCompletionSource<CommandResult> Done = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static double Ms(long from, long to) => (to - from) * 1000.0 / Stopwatch.Frequency;

    // ------------------------------------------------------------------
    // connecting and resuming
    // ------------------------------------------------------------------

    /// <summary>Opens the connection if it is not open (respecting the back-off unless <paramref name="now"/>).</summary>
    public async Task<bool> ConnectAsync(CancellationToken ct, bool now = false)
    {
        await connectLock.WaitAsync(ct);
        try
        {
            if (connection != null && connection.IsOpen) return true;
            if (giveUp) return false;
            if (!now && DateTime.UtcNow < nextConnect) return false;

            long cursor;
            lock (gate) cursor = lastApplied;
            var hello = new Hello { ClientVersion = o.ClientVersion, Token = connectionToken, ResumeId = resumeId, LastEventSequence = cursor };

            var started = Stopwatch.GetTimestamp();
            var (c, welcome, rejected, error) = await V2Connection.ConnectAsync(endpoint, invoker, hello, metrics, ct);
            var ms = Ms(started, Stopwatch.GetTimestamp());
            if (c == null || welcome == null)
            {
                var why = rejected != null ? "rejected: " + rejected.Reason + " (" + rejected.Message + ")" : error ?? "unknown";
                metrics.Endpoint("v2 connect (Hello to Welcome)").Record(ms, false, why);
                Interlocked.Increment(ref metrics.V2ConnectFailures);
                if (rejected != null)
                {
                    metrics.CountV2("rejected " + rejected.Reason);
                    if (!rejected.Reconnect && rejected.Reason is RejectReason.InvalidToken or RejectReason.ClientTooOld
                        or RejectReason.ProtocolMismatch or RejectReason.Disabled or RejectReason.Replaced)
                    {
                        giveUp = true;
                    }
                }
                if (!ct.IsCancellationRequested) log("v2: could not connect: " + why + (giveUp ? "; not trying again" : ", trying again in " + backoff.TotalSeconds + " s"));
                nextConnect = DateTime.UtcNow + backoff;
                backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 10));
                return false;
            }

            metrics.Endpoint("v2 connect (Hello to Welcome)").Record(ms, true);
            backoff = TimeSpan.FromSeconds(1);
            Interlocked.Increment(ref metrics.V2Connects);
            string? note = null;
            lock (gate)
            {
                unacked.Clear(); // acks for what went on the old connection will not come
                lastGameStatePlayerCount = -1;

                if (everWelcomed)
                {
                    if (welcome.Resumed)
                    {
                        Interlocked.Increment(ref metrics.V2Resumes);
                    }
                    else
                    {
                        Interlocked.Increment(ref metrics.V2ResumeFailures);
                        note = "v2: reconnected but the server did not resume the session (resync)";
                    }
                }

                if (welcome.EventSequence < lastApplied)
                {
                    note = "v2: the server's event sequence (" + welcome.EventSequence + ") is behind ours (" + lastApplied + "): it started over, so does our cursor";
                    lastApplied = 0;
                    lastAcked = 0;
                }
                replayUntil = everWelcomed && welcome.Resumed ? welcome.EventSequence : 0;
                // a resume acknowledges the Hello's cursor; otherwise acknowledge again with the next save
                lastAcked = welcome.Resumed ? Math.Min(cursor, lastApplied) : 0;

                if (welcome.ResyncRequired)
                {
                    Interlocked.Increment(ref metrics.V2Resyncs);
                    foreach (var p in byCharacter.Values) p.NeedsFull = true;
                }

                // acks for what went on the old connection may be lost: send everything unconfirmed at the next tick
                foreach (var p in byCharacter.Values) p.LastSentAt = DateTime.MinValue;

                everWelcomed = true;
                connection = c;
            }
            if (note != null) log(note);
            c.Start((m, size) => OnMessage(c, m, size));

            // Commands still waiting go again with the same id: the server answers a repeat with the first result.
            foreach (var pending in commands.Values)
            {
                if (pending.Done.Task.IsCompleted) continue;
                if (c.Send(pending.Request)) Interlocked.Increment(ref metrics.V2CommandResends);
            }
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            connectLock.Release();
        }
    }

    /// <summary>Notices a connection the server closed or that went silent, so the next tick reconnects.</summary>
    private void CheckConnection()
    {
        var c = connection;
        if (c == null) return;
        if (c.IsOpen && DateTime.UtcNow - c.LastReceived > TimeSpan.FromMilliseconds(ProtocolInfo.DeadAfterMs))
        {
            log("v2: nothing received for " + ProtocolInfo.DeadAfterMs / 1000 + " s, dropping the connection");
            metrics.CountV2("drop: silent for " + ProtocolInfo.DeadAfterMs / 1000 + " s");
            c.Abort();
            Interlocked.Increment(ref metrics.V2Drops);
            lock (gate) if (connection == c) connection = null;
            return;
        }

        if (!c.IsOpen)
        {
            lock (gate) if (connection == c) connection = null;
            if (!c.AbortedByUs)
            {
                Interlocked.Increment(ref metrics.V2Drops);
                metrics.CountV2("drop: " + (c.CloseReason ?? "closed"));
                log("v2: the connection closed (" + (c.CloseReason ?? "no reason") + "), reconnecting");
            }
        }
    }

    /// <summary>--v2-drop-every: drops the socket without a close, then resumes at once, as the game after a network blip.</summary>
    public async Task ChaosLoopAsync(CancellationToken ct)
    {
        if (o.V2DropEvery <= TimeSpan.Zero) return;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(o.V2DropEvery, ct);
                var c = connection;
                if (c == null || !c.IsOpen) continue;
                long before;
                lock (gate) before = lastApplied;
                c.Abort();
                Interlocked.Increment(ref metrics.V2ChaosDrops);
                downUntil = DateTime.UtcNow + o.V2DropPause;
                lock (gate) if (connection == c) connection = null; // the tick sees no connection while down
                // down for a while, as a real network blip: saves pile up in the outbox, events and acks on the server
                await Task.Delay(o.V2DropPause, ct);
                var resumesBefore = Interlocked.Read(ref metrics.V2Resumes);
                if (await ConnectAsync(ct, now: true))
                {
                    var resumed = Interlocked.Read(ref metrics.V2Resumes) > resumesBefore;
                    log("v2: dropped the connection (chaos) and " + (resumed ? "resumed" : "reconnected WITHOUT resuming") + " from event " + before);
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    // ------------------------------------------------------------------
    // incoming
    // ------------------------------------------------------------------

    private void OnMessage(V2Connection from, IMessage message, int size)
    {
        switch (message)
        {
            case Ping ping:
                from.Send(new Pong { Timestamp = ping.Timestamp });
                break;
            case Pong pong:
                var rtt = (DateTime.UtcNow.Ticks - pong.Timestamp) / (double)TimeSpan.TicksPerMillisecond;
                if (rtt >= 0 && rtt < 600_000) metrics.Endpoint("v2 ping").Record(rtt, true);
                break;
            case SaveAcks acks:
                OnAcks(acks);
                break;
            case EventBatch batch:
                OnEvents(batch, size);
                break;
            case CommandResult result:
                OnCommandResult(result);
                break;
            case ResyncRequired resync:
                metrics.CountV2("resync required (mid-session)");
                lock (gate)
                {
                    if (resync.CharacterIds == null || resync.CharacterIds.Length == 0)
                    {
                        foreach (var p in byCharacter.Values) p.NeedsFull = true;
                    }
                    else
                    {
                        foreach (var id in resync.CharacterIds)
                            if (byCharacter.TryGetValue(id, out var p)) p.NeedsFull = true;
                    }
                }
                break;
            case Rejected rejected:
                metrics.CountV2("rejected " + rejected.Reason);
                log("v2: the server closes the connection: " + rejected.Reason + " (" + rejected.Message + ")");
                if (!rejected.Reconnect && rejected.Reason == RejectReason.Replaced) giveUp = true;
                break;
            default:
                metrics.CountV2("unexpected " + message.GetType().Name);
                break;
        }
    }

    private void OnAcks(SaveAcks message)
    {
        if (message.Stage != SaveStage.Accepted && (message.Acks == null || message.Acks.Length == 0)) return;
        var now = Stopwatch.GetTimestamp();
        lock (gate)
        {
            if (message.Stage == SaveStage.Accepted)
            {
                // One answer per batch (ThroughSequence); the acks in it are only the refused saves.
                Interlocked.Increment(ref metrics.V2AcceptedAcks);
                while (unacked.Count > 0 && unacked.Peek().Sequence <= message.ThroughSequence)
                {
                    var b = unacked.Dequeue();
                    if (b.Sequence == message.ThroughSequence)
                    {
                        metrics.V2SaveRoundTrip.Record(Ms(b.SentAt, now), true);
                    }
                    else
                    {
                        Interlocked.Increment(ref metrics.V2BatchesWithoutAck);
                    }
                }

                foreach (var ack in message.Acks ?? Array.Empty<SaveAck>())
                {
                    // UnknownCharacter, NotInSession, Invalid: the protocol says drop it, do not retry
                    metrics.CountV2("save refused: " + ack.Status);
                    if (byCharacter.TryGetValue(ack.CharacterId, out var p)) MarkStored(p, ack.Version);
                }
            }
            else
            {
                Interlocked.Add(ref metrics.V2StoredAcks, message.Acks.Length);
                foreach (var ack in message.Acks)
                {
                    if (!byCharacter.TryGetValue(ack.CharacterId, out var p)) continue;
                    if (ack.Version <= p.StoredVersion) continue;
                    PendingSave? newest = null;
                    foreach (var e in p.Pending)
                    {
                        if (e.Version <= ack.Version && e.FirstSent != 0) newest = e;
                    }
                    if (newest != null) metrics.V2SaveStored.Record(Ms(newest.FirstSent, now), true);
                    MarkStored(p, ack.Version);
                }
            }
        }
    }

    private static void MarkStored(SimPlayer p, long version)
    {
        if (version <= p.StoredVersion) return;
        p.StoredVersion = version;
        if (version > p.AcceptedVersion) p.AcceptedVersion = version;
        p.Pending.RemoveAll(e => e.Version <= version);
    }

    private void OnEvents(EventBatch batch, int size)
    {
        Interlocked.Increment(ref metrics.EventBatches);
        Interlocked.Add(ref metrics.EventBytes, size);
        if (batch.Events == null) return;
        lock (gate)
        {
            foreach (var e in batch.Events)
            {
                if (e == null) continue;
                if (e.Sequence <= lastApplied)
                {
                    // harmless by design (the client skips it), but after a clean resume there should be none
                    Interlocked.Increment(ref metrics.V2EventRepeats);
                    continue;
                }

                if (e.Sequence > lastApplied + 1)
                {
                    var missing = e.Sequence - lastApplied - 1;
                    Interlocked.Add(ref metrics.V2EventGaps, missing);
                    if (eventGapsLogged++ < 5) log("v2: event gap: " + missing + " missing after " + lastApplied + " (got " + e.Sequence + ")");
                }

                if (e.Sequence <= replayUntil) Interlocked.Increment(ref metrics.V2EventReplays);
                lastApplied = e.Sequence;
                if (e.Type == (int)GameEventType.ItemAdd)
                {
                    Interlocked.Increment(ref metrics.V2ItemAddEvents);
                    if (e.Inventory != null) Interlocked.Increment(ref metrics.V2ItemAddWithInventory);
                }
                if (e.Inventory != null) ObserveRevision(e.Inventory);
                Interlocked.Increment(ref metrics.EventCount);
                metrics.CountEvent(Enum.IsDefined(typeof(GameEventType), e.Type) ? ((GameEventType)e.Type).ToString() : "Type" + e.Type);
            }
        }
    }

    /// <summary>
    /// Inventory revisions go up by one per change on the server. A jump is a change the game never heard of (a gap);
    /// a lower one than already seen means two paths (events, command results) delivered them out of order.
    /// Call under gate.
    /// </summary>
    private void ObserveRevision(InventoryUpdate update)
    {
        Interlocked.Increment(ref metrics.V2RevisionUpdates);
        if (!revisions.TryGetValue(update.CharacterId, out var last))
        {
            revisions[update.CharacterId] = update.Revision;
            return;
        }

        if (update.Revision <= last)
        {
            Interlocked.Increment(ref metrics.V2RevisionOutOfOrder);
            return;
        }

        if (update.Revision > last + 1)
        {
            Interlocked.Increment(ref metrics.V2RevisionGaps);
            Interlocked.Add(ref metrics.V2RevisionMissing, update.Revision - last - 1);
        }
        revisions[update.CharacterId] = update.Revision;
    }

    private void OnCommandResult(CommandResult result)
    {
        if (commands.TryGetValue(result.RequestId, out var pending))
        {
            if (!pending.Done.TrySetResult(result))
            {
                Interlocked.Increment(ref metrics.V2DuplicateResults);
            }
            else if (result.Inventory != null && !pending.IsRepeat)
            {
                lock (gate) foreach (var u in result.Inventory) ObserveRevision(u);
            }
        }
        else
        {
            // a second answer after a resend, or an answer after the time-out
            Interlocked.Increment(ref metrics.V2LateResults);
        }
    }

    // ------------------------------------------------------------------
    // saves
    // ------------------------------------------------------------------

    /// <summary>
    /// One tick: turns this tick's changes into new player versions, then sends every player with a new
    /// version, and again any player whose save is still not stored after --v2-resend-after (the game's
    /// SaveOutbox). Nothing is lost while disconnected: the outbox keeps it and the next connected tick
    /// sends it.
    /// </summary>
    public async Task TickAsync(List<SimPlayer> joined, CancellationToken ct)
    {
        CheckConnection();
        if (connection == null && DateTime.UtcNow >= downUntil) await ConnectAsync(ct);

        lock (gate)
        {
            // the backlog: players whose earlier saves are still not stored, before this tick adds versions
            var unconfirmed = 0;
            foreach (var p in joined)
            {
                if (p.CharacterId == Guid.Empty) continue;
                byCharacter.TryAdd(p.CharacterId, p);
                if (p.HasUnstoredSave) unconfirmed++;
                MakeVersion(p);
            }
            Unconfirmed = unconfirmed;
            metrics.V2Unconfirmed.Sample(unconfirmed);

            var c = connection;
            if (c == null || !c.IsOpen) return;

            var now = Stopwatch.GetTimestamp();
            var wall = DateTime.UtcNow;
            var saves = new List<PlayerSave>();
            foreach (var p in joined)
            {
                if (p.CharacterId == Guid.Empty || !p.HasUnstoredSave) continue;
                var fresh = p.Version > p.LastSentVersion;
                if (!fresh && wall - p.LastSentAt < o.V2ResendAfter) continue; // sent, waiting for Stored
                p.LastSentAt = wall;
                saves.Add(BuildSave(p, now));
            }

            GameStateSave? gs = null;
            if (joined.Count != lastGameStatePlayerCount)
            {
                gs = gameState(joined.Count);
                lastGameStatePlayerCount = joined.Count;
            }

            var ack = lastApplied > lastAcked ? lastApplied : 0;
            for (var start = 0; start < saves.Count || (start == 0 && gs != null); start += o.MaxBatch)
            {
                var players = saves.Skip(start).Take(o.MaxBatch).ToArray();
                var batch = new SaveBatch { Players = players, GameState = start == 0 ? gs : null, EventAck = ack, Sequence = ++batchSequence };
                if (players.Length > 0)
                {
                    // the server answers only batches with players
                    unacked.Enqueue(new SentBatch { Sequence = batch.Sequence, SentAt = now });
                }
                if (!c.Send(batch))
                {
                    unacked.Clear();
                    return; // closed: the outbox still has everything
                }
                Interlocked.Increment(ref metrics.V2SaveBatches);
                if (ack > 0)
                {
                    lastAcked = ack;
                    Interlocked.Increment(ref metrics.V2EventAcks);
                    ack = 0;
                }
                if (players.Length == 0) break;
            }

            if (lastApplied > lastAcked && c.Send(new EventAck { Sequence = lastApplied }))
            {
                lastAcked = lastApplied;
                Interlocked.Increment(ref metrics.V2EventAcks);
            }

            if (DateTime.UtcNow - c.LastSent > TimeSpan.FromMilliseconds(ProtocolInfo.PingIntervalMs))
            {
                c.Send(new Ping { Timestamp = DateTime.UtcNow.Ticks });
            }
        }
    }

    /// <summary>Gives the player a new version when something changed since the last one.</summary>
    private static void MakeVersion(SimPlayer p)
    {
        var skillMask = p.ExpDirty;
        p.ExpDirty = 0;
        var current = p.CurrentState();
        var stateMask = p.StateDirtyMask(current) & (uint)PlayerStateFields.All;
        p.LastSentState = current;
        var full = p.NeedsFull;
        if (skillMask == 0 && stateMask == 0 && !full) return;

        p.NeedsFull = false;
        p.Version++;
        double[]? gained = null;
        if (skillMask != 0)
        {
            gained = new double[SimPlayer.SkillCount];
            for (var s = 0; s < SimPlayer.SkillCount; ++s)
            {
                gained[s] = p.GainedSinceVersion[s];
                p.GainedSinceVersion[s] = 0;
            }
        }
        p.Pending.Add(new PendingSave { Version = p.Version, SkillMask = skillMask, StateMask = stateMask, Full = full, Gained = gained });
    }

    /// <summary>Everything the player's unstored versions changed, with the current values, under the newest version.</summary>
    private PlayerSave BuildSave(SimPlayer p, long now)
    {
        uint skillMask = 0, stateMask = 0;
        var full = false;
        foreach (var e in p.Pending)
        {
            skillMask |= e.SkillMask;
            stateMask |= e.StateMask;
            full |= e.Full;
            if (e.FirstSent == 0) e.FirstSent = now;
        }
        if (full)
        {
            skillMask = AllSkills;
            stateMask = (uint)PlayerStateFields.All;
        }

        if (p.LastSentVersion >= p.Version) Interlocked.Increment(ref metrics.V2Resends);
        p.LastSentVersion = p.Version;

        var skills = new List<SkillSave>();
        for (var s = 0; s < SimPlayer.SkillCount; ++s)
        {
            if ((skillMask & (1u << s)) == 0) continue;
            double gained = 0;
            foreach (var e in p.Pending) if (e.Gained != null) gained += e.Gained[s];
            skills.Add(new SkillSave { Index = (byte)s, Level = p.Level[s], Experience = p.Exp[s], Gained = gained });
            // what the verification compares with: the server must end up with at least this
            p.SentLevel[s] = p.Level[s];
            p.SentExp[s] = (long)Math.Floor(p.Exp[s]);
        }

        PlayerStateSave? state = null;
        if (stateMask != 0)
        {
            state = new PlayerStateSave
            {
                Changed = (PlayerStateFields)stateMask,
                Health = p.Health,
                Island = (byte)p.Island,
                Destination = (byte)Island.None,
                State = 0,
                TrainingSkillIndex = p.TrainingSkill,
                TaskArgument = p.TaskArgument,
                ExpPerHour = p.ExpPerHour,
                EstimatedTimeForLevelUp = p.LevelUpEta,
                X = p.X,
                Y = p.Y,
                Z = p.Z,
                Platform = "twitch",
                PlatformUserId = p.PlatformId,
                PlatformUserName = p.UserName
            };
        }

        Interlocked.Increment(ref metrics.V2PlayerSaves);
        Interlocked.Add(ref metrics.V2SkillEntries, skills.Count);
        if (full) Interlocked.Increment(ref metrics.V2FullSaves);
        return new PlayerSave { CharacterId = p.CharacterId, Version = p.Version, Full = full, Skills = skills.Count > 0 ? skills.ToArray() : null, State = state };
    }

    /// <summary>
    /// The game's quit: keeps sending until every save is stored or the time is up, so the verification
    /// compares with what the server was given a fair chance to store.
    /// </summary>
    public async Task<int> DrainAsync(SimPlayer[] players, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        var joined = players.Where(p => p.Joined).ToList();
        lock (gate) foreach (var p in joined) p.LastSentAt = DateTime.MinValue; // quitting sends everything unconfirmed at once
        int left;
        while (true)
        {
            await TickAsync(joined, ct);
            lock (gate) left = joined.Count(p => p.HasUnstoredSave);
            if (left == 0 || clock.Elapsed >= o.V2DrainTimeout || ct.IsCancellationRequested) break;
            try { await Task.Delay(TimeSpan.FromMilliseconds(500), ct); } catch (OperationCanceledException) { break; }
        }
        lock (metrics)
        {
            metrics.V2DrainSeconds = Math.Max(metrics.V2DrainSeconds ?? 0, clock.Elapsed.TotalSeconds);
            metrics.V2LeftUnstored += left;
        }
        return left;
    }

    // ------------------------------------------------------------------
    // commands
    // ------------------------------------------------------------------

    /// <summary>
    /// Sends a command and waits for its result. Null when no answer came in time. A command sent while
    /// disconnected goes when the connection is back, with the same request id.
    /// </summary>
    public async Task<CommandResult?> SendCommandAsync(string command, byte[] args, CancellationToken ct, Guid? requestId = null, string? endpointName = null)
    {
        var request = new CommandRequest { RequestId = requestId ?? Guid.NewGuid(), Command = command, Args = args };
        var pending = new PendingCommand { Request = request, IsRepeat = endpointName != null && endpointName.EndsWith("(repeat)") };
        var name = endpointName ?? "v2 " + command;
        if (!commands.TryAdd(request.RequestId, pending))
        {
            metrics.Endpoint(name).Record(0, false, "request id already waiting");
            return null;
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            connection?.Send(request);
            CommandResult result;
            try
            {
                result = await pending.Done.Task.WaitAsync(TimeSpan.FromSeconds(o.RequestTimeoutSeconds), ct);
            }
            catch (TimeoutException)
            {
                metrics.Endpoint(name).Record(Ms(started, Stopwatch.GetTimestamp()), false, "no answer within " + o.RequestTimeoutSeconds + " s");
                return null;
            }

            var ok = result.Status is CommandStatus.Ok or CommandStatus.Accepted;
            metrics.Endpoint(name).Record(Ms(started, Stopwatch.GetTimestamp()), ok, ok ? null : result.Status + ": " + result.Error);
            metrics.CountV2("command " + command + " " + result.Status);
            return result;
        }
        finally
        {
            commands.TryRemove(request.RequestId, out _);
        }
    }

    public static T? Value<T>(CommandResult? result)
    {
        if (result?.Value == null) return default;
        return ProtocolSerializer.DeserializeValue<T>(result.Value);
    }

    /// <summary>The join answer in the shape the REST join returns, for the session's usual join handling.</summary>
    public static JObject ToJson(PlayerJoinResult r)
    {
        var json = new JObject
        {
            ["success"] = r.Success,
            ["isNewUser"] = r.IsNewUser,
            ["errorMessage"] = r.ErrorMessage
        };
        if (r.Player != null)
        {
            json["player"] = new JObject
            {
                ["id"] = r.Player.Id.ToString(),
                ["userId"] = r.Player.UserId.ToString(),
                ["skills"] = r.Player.Skills != null ? JObject.FromObject(r.Player.Skills) : null
            };
        }
        return json;
    }

    /// <summary>
    /// --v2-repeat-share: the same request again. The answer must be the first one, field for field,
    /// and the inventory changes must carry the same revision (the server did not run it twice).
    /// </summary>
    public async Task<(CommandResult? Again, bool Same)> RepeatAsync(CommandRequest first, CommandResult firstResult, CancellationToken ct)
    {
        Interlocked.Increment(ref metrics.V2CommandRepeats);
        var again = await SendCommandAsync(first.Command, first.Args, ct, first.RequestId, "v2 " + first.Command + " (repeat)");
        if (again == null) return (null, false);
        var same = SameResult(firstResult, again, out var why);
        if (same)
        {
            Interlocked.Increment(ref metrics.V2RepeatMatches);
        }
        else
        {
            Interlocked.Increment(ref metrics.V2RepeatMismatches);
            if (Interlocked.Increment(ref mismatchesLogged) <= 5) log("v2: repeated " + first.Command + " answered differently: " + why);
        }
        return (again, same);
    }

    private static bool SameResult(CommandResult a, CommandResult b, out string why)
    {
        why = "";
        if (a.Status != b.Status) { why = "status " + a.Status + " then " + b.Status; return false; }
        if (a.Error != b.Error) { why = "error text differs"; return false; }
        if (!(a.Value ?? Array.Empty<byte>()).AsSpan().SequenceEqual(b.Value ?? Array.Empty<byte>())) { why = "value differs"; return false; }
        var ai = a.Inventory ?? Array.Empty<InventoryUpdate>();
        var bi = b.Inventory ?? Array.Empty<InventoryUpdate>();
        if (ai.Length != bi.Length) { why = "inventory updates " + ai.Length + " then " + bi.Length; return false; }
        for (var i = 0; i < ai.Length; ++i)
        {
            if (ai[i].CharacterId != bi[i].CharacterId || ai[i].Revision != bi[i].Revision)
            {
                why = "inventory revision " + ai[i].Revision + " then " + bi[i].Revision;
                return false;
            }
            var sa = ai[i].Stacks ?? Array.Empty<InventoryStack>();
            var sb = bi[i].Stacks ?? Array.Empty<InventoryStack>();
            if (sa.Length != sb.Length) { why = "stack count differs"; return false; }
            for (var j = 0; j < sa.Length; ++j)
            {
                if (sa[j].Id != sb[j].Id || sa[j].Amount != sb[j].Amount) { why = "stack " + sa[j].Id + " amount " + sa[j].Amount + " then " + sb[j].Amount; return false; }
            }
        }
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        var c = connection;
        connection = null;
        if (c != null) await c.DisposeAsync();
        invoker.Dispose();
    }
}
