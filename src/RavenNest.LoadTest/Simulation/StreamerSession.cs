using System.Diagnostics;
using Newtonsoft.Json.Linq;
using RavenNest.LoadTest.Delta;
using RavenNest.LoadTest.Events;
using RavenNest.LoadTest.Rest;
using RavenNest.Models;
using RavenNest.Protocol;

namespace RavenNest.LoadTest.Simulation;

/// <summary>
/// One simulated streamer: signs in, begins a game session, keeps both sockets up (or with
/// --protocol v2 the one v2 connection), joins the players, sends deltas or v2 saves every tick and
/// optionally fires commands.
/// </summary>
public sealed class StreamerSession : IAsyncDisposable
{
    private readonly int index;
    private readonly Options o;
    private readonly Metrics metrics;
    private readonly RestApi api;
    private readonly Random rng;
    private readonly object rngLock = new();
    private DeltaConnection? delta;
    private EventConnection? events;
    private V2Link? v2;
    private string? sessionTokenB64;
    private DateTime lastDeltaReconnect = DateTime.MinValue;
    private int lastGameStatePlayerCount = -1;
    private DateTime nextRaid, nextDungeon;
    private List<Guid> itemIds = new();
    private List<Guid> recipeIds = new();
    private long ticks, tickOverruns;
    private double worstTickMs;

    public StreamerSession(int index, Options options, Metrics metrics)
    {
        this.index = index;
        o = options;
        this.metrics = metrics;
        Name = options.StreamerNameFor(index);
        api = new RestApi(options, metrics);
        rng = new Random(1000 + index);
        Players = Enumerable.Range(1, options.Players)
            .Select(n => new SimPlayer(n,
                (options.PlayerPrefix + "_s" + (index + 1) + "_" + n.ToString("D5")).ToLowerInvariant(),
                options.PlayerPrefix + "-s" + (index + 1) + "-" + n.ToString("D5")))
            .ToArray();
    }

    public string Name { get; }
    public SimPlayer[] Players { get; }
    public Guid SessionId { get; private set; }
    public DateTime? JoinStarted { get; private set; }
    public DateTime? JoinFinished { get; private set; }
    public long Ticks => ticks;
    public long TickOverruns => tickOverruns;
    public double WorstTickMs => worstTickMs;
    public bool DeltaConnected => delta?.Connected ?? false;
    public bool EventsConnected => events?.IsConnected ?? false;
    public bool V2Connected => v2?.Connected ?? false;
    public int V2Unconfirmed => v2?.Unconfirmed ?? 0;
    public int JoinedCount => Players.Count(p => p.Joined);

    private void Log(string message) => Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " [" + Name + "] " + message);

    private double NextDouble()
    {
        lock (rngLock) return rng.NextDouble();
    }

    private int Next(int max)
    {
        lock (rngLock) return rng.Next(max);
    }

    // ------------------------------------------------------------------
    // start
    // ------------------------------------------------------------------

    public async Task<bool> StartAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(o.AuthToken) && o.Sessions == 1)
        {
            api.AuthTokenHeader = o.AuthToken.Trim();
            Log("using the auth token from the command line / environment");
        }
        else
        {
            var (token, error) = await api.AuthenticateAsync(Name, o.Password!, ct);
            if (token == null)
            {
                Log("sign in failed: " + error + ". Has the seed been run and the server restarted since? (see README)");
                return false;
            }
            api.AuthTokenHeader = RestApi.Encode(token);
            Log("signed in");
        }

        var (result, beginError) = await api.BeginSessionAsync(o.ClientVersion, o.AccessKey!, ct);
        if (result?.SessionToken == null)
        {
            Log("could not begin a session: " + beginError);
            return false;
        }

        // The game keeps the token object and sends it re-serialised (Newtonsoft, PascalCase) in the
        // session-token header, the delta auth frame and the event socket auth packet.
        sessionTokenB64 = RestApi.Encode(result.SessionToken);
        api.SessionTokenHeader = sessionTokenB64;
        SessionId = result.SessionToken.SessionId;
        Log("session started");

        var now = DateTime.UtcNow;
        nextRaid = now.AddMinutes(15 + index);
        nextDungeon = now.AddMinutes(20 + index);

        if (o.Protocol == WireProtocol.V2)
        {
            // v2 replaces both sockets: saves, events and (with --v2-commands) commands go on one WebSocket
            if (string.IsNullOrEmpty(result.ConnectionToken))
            {
                Log("BeginSession returned no ConnectionToken: this server has no v2 connection. Run it from a branch with network v2, or use --protocol v1.");
                return false;
            }

            // the game can also fetch its token here (older models drop the BeginSession field): it must be the same kind
            var fetched = await api.SendAsync("GET game/connection-token", HttpMethod.Get, "game/connection-token", null, ct);
            var other = fetched.Ok ? fetched.Json()?.Type == JTokenType.String ? fetched.Json()!.ToString() : null : null;
            metrics.CountV2(other == null ? "connection-token endpoint: no token (" + (fetched.Error ?? "not a JSON string") + ")"
                : other == result.ConnectionToken ? "connection-token endpoint: same token as BeginSession"
                : other.Length == result.ConnectionToken.Length ? "connection-token endpoint: a different token of the same kind"
                : "connection-token endpoint: a token of another kind");

            v2 = new V2Link(o, metrics, Log, result.ConnectionToken, BuildGameState);
            if (!await v2.ConnectAsync(ct, now: true))
            {
                Log("could not open the v2 connection");
                return false;
            }
            Log("v2 connection open (" + V2.V2Connection.EndpointFor(o.BaseUrl) + ")");
        }
        else
        {
            events = new EventConnection(o.EffectiveTcpHost, o.EventPort, () => sessionTokenB64!, metrics, Log);
            events.Start(ct);

            delta = new DeltaConnection(o.EffectiveTcpHost, o.DeltaPort, () => DeltaConnection.AuthPayload(sessionTokenB64!), metrics, Log);
            delta.Disconnected += OnDeltaDisconnected;
            lastDeltaReconnect = DateTime.UtcNow;
            if (await delta.ConnectAsync(ct))
            {
                Log("delta socket connected");
            }
        }

        if (o.CommandRate > 0)
        {
            await LoadCommandDataAsync(ct);
        }

        return true;
    }

    private GameStateSave BuildGameState(int playerCount) => new GameStateSave
    {
        PlayerCount = playerCount,
        Raid = new RaidInfo { NextRaid = nextRaid },
        Dungeon = new DungeonInfo { NextDungeon = nextDungeon }
    };

    private void OnDeltaDisconnected()
    {
        // DeltaClientBehaviour.OnDisconnected: forget what was sent, so everything goes again on reconnect
        foreach (var p in Players)
        {
            p.LastSentState = null;
        }
        lastGameStatePlayerCount = -1;
    }

    // ------------------------------------------------------------------
    // players
    // ------------------------------------------------------------------

    public async Task JoinPlayersAsync(CancellationToken ct)
    {
        try
        {
            await JoinPlayersCoreAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            JoinFinished ??= DateTime.UtcNow;
            Roster.Save(o.RosterFolder, Name, Players);
            Log("join phase cut short by the end of the run: " + JoinedCount + "/" + Players.Length + " players joined");
        }
    }

    private async Task JoinPlayersCoreAsync(CancellationToken ct)
    {
        JoinStarted = DateTime.UtcNow;
        var pending = Players.ToList();

        if (o.JoinMode == JoinMode.Restore)
        {
            var roster = Roster.Load(o.RosterFolder, Name);
            var known = pending.Where(p => roster.ContainsKey(p.UserName)).ToList();
            foreach (var p in known) p.CharacterId = roster[p.UserName];
            if (known.Count > 0)
            {
                await RestoreAsync(known, ct);
            }
            else
            {
                Log("no roster of earlier characters, joining everyone instead");
            }
            pending = pending.Where(p => !p.Joined).ToList();
        }

        for (var i = 0; i < pending.Count && !ct.IsCancellationRequested; i += o.JoinConcurrency)
        {
            var batch = pending.Skip(i).Take(o.JoinConcurrency);
            await Task.WhenAll(batch.Select(p => JoinAsync(p, ct)));
            if (o.JoinDelayMs > 0 && i + o.JoinConcurrency < pending.Count)
            {
                try { await Task.Delay(o.JoinDelayMs, ct); } catch (OperationCanceledException) { break; }
            }
        }

        JoinFinished = DateTime.UtcNow;
        Roster.Save(o.RosterFolder, Name, Players);
        Log("join phase done: " + JoinedCount + "/" + Players.Length + " players in " + (JoinFinished - JoinStarted)!.Value.TotalSeconds.ToString("0.0") + " s");
    }

    private async Task JoinAsync(SimPlayer p, CancellationToken ct)
    {
        const string endpoint = "POST players (join)";
        var body = new PlayerJoinData
        {
            UserName = p.UserName,
            PlatformId = p.PlatformId,
            Platform = "twitch",
            Identifier = "1",
        };

        if (v2 != null && o.V2Commands)
        {
            await JoinV2Async(p, body, ct);
            return;
        }

        // RavenNestClient.PlayerJoinAsync retries a few times on a network error
        for (var attempt = 0; attempt < 3 && !ct.IsCancellationRequested; ++attempt)
        {
            ApiResult r;
            try
            {
                r = await api.SendAsync(endpoint, HttpMethod.Post, "players", body, ct, record: false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!r.Ok && r.Status == 0 && attempt < 2)
            {
                api.RecordOutcome(endpoint, r, false, r.Error + " (retried)");
                await Task.Delay(250 * (attempt + 1), ct).ContinueWith(_ => { });
                continue;
            }

            if (!r.Ok)
            {
                api.RecordOutcome(endpoint, r, false, r.Error);
                Interlocked.Increment(ref metrics.PlayersJoinFailed);
                return;
            }

            var json = r.Json();
            if (!ApplyJoinResult(p, json, out var error))
            {
                api.RecordOutcome(endpoint, r, false, "join refused: " + error);
                Interlocked.Increment(ref metrics.PlayersJoinFailed);
                return;
            }

            api.RecordOutcome(endpoint, r, true, null);
            return;
        }
    }

    private async Task JoinV2Async(SimPlayer p, PlayerJoinData body, CancellationToken ct)
    {
        // a join that timed out is asked again with the same request id, so it cannot join twice
        var id = Guid.NewGuid();
        for (var attempt = 0; attempt < 3 && !ct.IsCancellationRequested; ++attempt)
        {
            CommandResult? r;
            try
            {
                r = await v2!.SendCommandAsync(RavenNest.Protocol.Commands.PlayerJoin, CommandArgs.Pack(body), ct, id);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (r == null) continue;
            if (r.Status is CommandStatus.Busy or CommandStatus.Error && attempt < 2)
            {
                await Task.Delay(250 * (attempt + 1), ct).ContinueWith(_ => { });
                continue;
            }

            PlayerJoinResult? value = null;
            try { value = V2Link.Value<PlayerJoinResult>(r); } catch (Exception exc) { metrics.CountV2("join answer unreadable: " + exc.GetType().Name); }
            if (r.Status != CommandStatus.Ok || value == null || !ApplyJoinResult(p, V2Link.ToJson(value), out var error))
            {
                metrics.CountV2("join refused: " + (value?.ErrorMessage ?? r.Error ?? r.Status.ToString()));
                Interlocked.Increment(ref metrics.PlayersJoinFailed);
            }
            return;
        }
        Interlocked.Increment(ref metrics.PlayersJoinFailed);
    }

    private async Task RestoreAsync(List<SimPlayer> known, CancellationToken ct)
    {
        const string endpoint = "POST players/restore";
        var body = new PlayerRestoreData { Characters = known.Select(p => p.CharacterId).ToArray(), ForceAdd = false };
        if (v2 != null && o.V2Commands)
        {
            await RestoreV2Async(known, body, ct);
            return;
        }

        var r = await api.SendAsync(endpoint, HttpMethod.Post, "players/restore", body, ct, record: false);
        if (!r.Ok)
        {
            api.RecordOutcome(endpoint, r, false, r.Error);
            Log("restore failed: " + r.Error);
            return;
        }

        var results = r.Json().Get("players") as JArray;
        var byCharacter = known.ToDictionary(p => p.CharacterId);
        var restored = 0;
        if (results != null)
        {
            foreach (var item in results)
            {
                var id = item.Get("player").GuidOf("id");
                if (id != Guid.Empty && byCharacter.TryGetValue(id, out var p) && ApplyJoinResult(p, item, out _))
                {
                    restored++;
                }
            }
        }

        api.RecordOutcome(endpoint, r, restored > 0, restored > 0 ? null : "restore returned no players");
        Interlocked.Add(ref metrics.PlayersRestored, restored);
        Log("restored " + restored + "/" + known.Count + " players in one request (" + r.Milliseconds.ToString("0") + " ms)");
    }

    private async Task RestoreV2Async(List<SimPlayer> known, PlayerRestoreData body, CancellationToken ct)
    {
        var started = Stopwatch.StartNew();
        var r = await v2!.SendCommandAsync(RavenNest.Protocol.Commands.PlayerRestore, CommandArgs.Pack(body), ct);
        PlayerRestoreResult? value = null;
        try { value = V2Link.Value<PlayerRestoreResult>(r); } catch (Exception exc) { metrics.CountV2("restore answer unreadable: " + exc.GetType().Name); }
        var byCharacter = known.ToDictionary(p => p.CharacterId);
        var restored = 0;
        foreach (var item in value?.Players ?? Array.Empty<PlayerJoinResult>())
        {
            if (item?.Player != null && byCharacter.TryGetValue(item.Player.Id, out var p) && ApplyJoinResult(p, V2Link.ToJson(item), out _))
            {
                restored++;
            }
        }
        Interlocked.Add(ref metrics.PlayersRestored, restored);
        Log("restored " + restored + "/" + known.Count + " players in one v2 command (" + started.Elapsed.TotalMilliseconds.ToString("0") + " ms)");
    }

    private bool ApplyJoinResult(SimPlayer p, JToken? json, out string error)
    {
        error = "";
        if (json == null)
        {
            error = "empty answer";
            return false;
        }

        if (!json.Bool("success"))
        {
            error = json.Str("errorMessage") ?? "success = false";
            return false;
        }

        var player = json.Get("player");
        var id = player.GuidOf("id");
        if (id == Guid.Empty)
        {
            error = "no character in the answer";
            return false;
        }

        p.CharacterId = id;
        p.UserId = player.GuidOf("userId");
        p.IsNewUser = json.Bool("isNewUser");
        p.LoadSkills(player.Get("skills"));
        lock (rngLock)
        {
            p.PickTraining(rng);
            p.X = (short)rng.Next(-200, 200);
            p.Z = (short)rng.Next(-200, 200);
        }
        p.Joined = true;
        Interlocked.Increment(ref metrics.PlayersJoined);
        if (p.IsNewUser) Interlocked.Increment(ref metrics.PlayersNew);
        return true;
    }

    // ------------------------------------------------------------------
    // deltas
    // ------------------------------------------------------------------

    public async Task TickLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(o.Tick);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    await TickAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exc)
                {
                    Log("tick failed: " + exc.Message);
                }

                Interlocked.Increment(ref ticks);
                var ms = sw.Elapsed.TotalMilliseconds;
                if (ms > worstTickMs) worstTickMs = ms;
                if (ms > o.Tick.TotalMilliseconds) Interlocked.Increment(ref tickOverruns);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        if (v2 != null)
        {
            var players = Players.Where(p => p.Joined).ToList();
            if (players.Count > 0) Simulate(players);
            await v2.TickAsync(players, ct);
            return;
        }

        if (delta == null) return;

        if (!delta.Connected)
        {
            // DeltaClientBehaviour tries again every 5 seconds
            if (DateTime.UtcNow - lastDeltaReconnect < TimeSpan.FromSeconds(5)) return;
            lastDeltaReconnect = DateTime.UtcNow;
            OnDeltaDisconnected();
            if (!await delta.ConnectAsync(ct)) return;
            Log("delta socket reconnected");
        }

        var joined = Players.Where(p => p.Joined).ToList();
        if (joined.Count == 0) return;

        Simulate(joined);
        await SendExperienceAsync(joined, ct);
        await SendStateAsync(joined, ct);
        await SendGameStateAsync(joined.Count, ct);
    }

    private void Simulate(List<SimPlayer> joined)
    {
        var now = DateTime.UtcNow;
        lock (rngLock)
        {
            foreach (var p in joined)
            {
                if (p.TrainingSkill < 0) p.PickTraining(rng);

                double gain = 0;
                if (rng.NextDouble() < o.ExpShare)
                {
                    var lvl = p.Level[p.TrainingSkill];
                    gain = o.ExpRate * (1 + lvl / 20.0) * (0.5 + rng.NextDouble());
                    p.GainExp(p.TrainingSkill, gain);
                    if (p.SecondarySkill >= 0)
                    {
                        p.GainExp(p.SecondarySkill, gain / 3);
                    }
                }

                if (rng.NextDouble() < o.StateShare)
                {
                    // The game recomputes exp per hour and the level up estimate every tick for a
                    // training player, so those two change almost every time; movement and health often.
                    if (gain > 0)
                    {
                        p.ExpPerHour = (long)(gain * (3600 / o.Tick.TotalSeconds));
                        var next = RavenNest.BusinessLogic.GameMath.ExperienceForLevel(p.Level[p.TrainingSkill] + 1) - p.Exp[p.TrainingSkill];
                        p.LevelUpEta = now.AddSeconds(Math.Min(next / Math.Max(1, gain) * o.Tick.TotalSeconds, 86400 * 365));
                    }
                    if (rng.NextDouble() < 0.5)
                    {
                        p.X = (short)Math.Clamp(p.X + rng.Next(-3, 4), short.MinValue, short.MaxValue);
                        p.Z = (short)Math.Clamp(p.Z + rng.Next(-3, 4), short.MinValue, short.MaxValue);
                    }
                    if (p.SecondarySkill >= 0 && rng.NextDouble() < 0.3)
                    {
                        var max = Math.Max(1, p.Level[(int)Skill.Health]);
                        p.Health = (short)Math.Clamp(rng.Next(1, max + 1), 1, short.MaxValue);
                    }
                    if (rng.NextDouble() < 0.01)
                    {
                        p.PickTraining(rng);
                    }
                }
            }
        }
    }

    private async Task SendExperienceAsync(List<SimPlayer> joined, CancellationToken ct)
    {
        var dirty = joined.Where(p => p.ExpDirty != 0).ToList();
        for (var start = 0; start < dirty.Count; start += o.MaxBatch)
        {
            var batch = dirty.Skip(start).Take(o.MaxBatch).ToList();
            var updates = new DeltaExperienceUpdate[batch.Count];
            var skillEntries = 0;
            for (var i = 0; i < batch.Count; ++i)
            {
                var p = batch[i];
                var changes = new List<SkillDelta>();
                for (var s = 0; s < SimPlayer.SkillCount; ++s)
                {
                    if ((p.ExpDirty & (1u << s)) == 0) continue;
                    changes.Add(new SkillDelta { Index = (byte)s, Experience = (long)p.Exp[s], Level = (short)p.Level[s] });
                }
                skillEntries += changes.Count;
                updates[i] = new DeltaExperienceUpdate { CharacterId = p.CharacterId, DirtyMask = p.ExpDirty, Changes = changes.ToArray() };
            }

            var buffer = new byte[DeltaWriter.GetExpBufferSize(batch.Count)];
            var length = DeltaWriter.Serialize(updates, buffer, batch.Count);
            if (!await delta!.SendFrameAsync(DeltaFrameType.Experience, buffer, length, ct))
            {
                // not written: keep them dirty so the next tick sends the newest values
                return;
            }

            Interlocked.Increment(ref metrics.DeltaExpFrames);
            Interlocked.Add(ref metrics.DeltaExpEntries, batch.Count);
            Interlocked.Add(ref metrics.DeltaSkillEntries, skillEntries);
            for (var i = 0; i < batch.Count; ++i)
            {
                var p = batch[i];
                foreach (var c in updates[i].Changes)
                {
                    p.SentLevel[c.Index] = c.Level;
                    p.SentExp[c.Index] = c.Experience;
                }
                p.ExpDirty = 0;
            }

            if (start + o.MaxBatch < dirty.Count)
            {
                await Task.Delay(10, ct); // as the game, between large batches
            }
        }
    }

    private async Task SendStateAsync(List<SimPlayer> joined, CancellationToken ct)
    {
        var changed = new List<(SimPlayer Player, CharacterStateDelta State)>();
        foreach (var p in joined)
        {
            var current = p.CurrentState();
            var mask = p.StateDirtyMask(current);
            if (mask == 0) continue;
            current.DirtyMask = mask;
            changed.Add((p, current));
        }

        for (var start = 0; start < changed.Count; start += o.MaxBatch)
        {
            var batch = changed.Skip(start).Take(o.MaxBatch).ToList();
            var states = batch.Select(x => x.State).ToArray();
            var buffer = new byte[DeltaWriter.GetPlayerStateBufferSize(states.Length)];
            var length = DeltaWriter.Serialize(states, buffer, states.Length);
            if (!await delta!.SendFrameAsync(DeltaFrameType.PlayerState, buffer, length, ct))
            {
                return;
            }

            Interlocked.Increment(ref metrics.DeltaStateFrames);
            Interlocked.Add(ref metrics.DeltaStateEntries, states.Length);
            foreach (var (player, state) in batch)
            {
                player.LastSentState = state;
            }
            await Task.Delay(10, ct);
        }
    }

    private async Task SendGameStateAsync(int playerCount, CancellationToken ct)
    {
        // DeltaClientBehaviour.ShouldSendGameState: first time, then on a player count change
        if (playerCount == lastGameStatePlayerCount) return;

        var gs = new GameStateFrame { PlayerCount = playerCount };
        gs.Raid.NextRaid = nextRaid;
        gs.Dungeon.NextDungeon = nextDungeon;
        var buffer = new byte[DeltaWriter.MaxGameStateSize];
        var length = DeltaWriter.Serialize(gs, buffer);
        if (await delta!.SendFrameAsync(DeltaFrameType.GameState, buffer, length, ct))
        {
            Interlocked.Increment(ref metrics.DeltaGameStateFrames);
            lastGameStatePlayerCount = playerCount;
        }
    }

    // ------------------------------------------------------------------
    // REST commands
    // ------------------------------------------------------------------

    private async Task LoadCommandDataAsync(CancellationToken ct)
    {
        if (o.Commands.Contains("additem") || o.Commands.Contains("craft"))
        {
            var r = await api.SendAsync("GET items", HttpMethod.Get, "items", null, ct);
            if (r.Json() is JArray items)
            {
                itemIds = items.Select(x => x.GuidOf("id")).Where(x => x != Guid.Empty).ToList();
            }
            Log("loaded " + itemIds.Count + " items for commands");
        }

        if (o.Commands.Contains("produce"))
        {
            var r = await api.SendAsync("GET items/recipes", HttpMethod.Get, "items/recipes", null, ct);
            if (r.Json() is JArray recipes)
            {
                recipeIds = recipes.Select(x => x.GuidOf("id")).Where(x => x != Guid.Empty).ToList();
            }
            Log("loaded " + recipeIds.Count + " recipes for commands");
        }
    }

    public async Task CommandLoopAsync(CancellationToken ct)
    {
        if (o.CommandRate <= 0 || o.Commands.Length == 0) return;

        var inFlight = new SemaphoreSlim(256);
        var interval = TimeSpan.FromSeconds(1.0 / o.CommandRate);
        var clock = Stopwatch.StartNew();
        var due = TimeSpan.Zero;
        var running = new List<Task>();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                due += interval;
                var wait = due - clock.Elapsed;
                if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);

                var joined = Players.Where(p => p.Joined).ToList();
                if (joined.Count == 0) continue;

                if (!inFlight.Wait(0))
                {
                    metrics.Endpoint("commands skipped (256 in flight)").Record(0, false, "the server is not keeping up with the command rate");
                    continue;
                }

                var player = joined[Next(joined.Count)];
                for (var tries = 0; player.CommandBusy && tries < 8; ++tries)
                {
                    player = joined[Next(joined.Count)];
                }
                var command = o.Commands[Next(o.Commands.Length)];
                running.Add(Task.Run(async () =>
                {
                    try { await RunCommandAsync(command, player, ct); }
                    catch { }
                    finally { inFlight.Release(); }
                }, CancellationToken.None));
                running.RemoveAll(t => t.IsCompleted);
            }
        }
        catch (OperationCanceledException)
        {
        }

        try { await Task.WhenAll(running).WaitAsync(TimeSpan.FromSeconds(o.RequestTimeoutSeconds)); } catch { }
    }

    private Task RunCommandAsync(string command, SimPlayer p, CancellationToken ct)
    {
        if (v2 != null && o.V2Commands && command != "fetch")
        {
            return RunCommandV2Async(command, p, ct);
        }

        var cid = p.CharacterId;
        switch (command)
        {
            case "additem" when itemIds.Count > 0:
                return api.SendAsync("GET players/v2/{id}/item/{item}", HttpMethod.Get, "players/v2/" + cid + "/item/" + itemIds[Next(itemIds.Count)], null, ct);
            case "craft" when itemIds.Count > 0:
                return api.SendAsync("GET players/v2/{id}/craft/{item}/1", HttpMethod.Get, "players/v2/" + cid + "/craft/" + itemIds[Next(itemIds.Count)] + "/1", null, ct);
            case "produce" when recipeIds.Count > 0:
                return api.SendAsync("GET players/{id}/produce/{recipe}/1", HttpMethod.Get, "players/" + cid + "/produce/" + recipeIds[Next(recipeIds.Count)] + "/1", null, ct);
            case "equipall":
                return api.SendAsync("GET players/v2/{id}/equipall", HttpMethod.Get, "players/v2/" + cid + "/equipall", null, ct);
            case "highscore":
                return api.SendAsync("GET players/highscore/{id}/all", HttpMethod.Get, "players/highscore/" + cid + "/all", null, ct);
            default:
                return api.SendAsync("GET players/{id}", HttpMethod.Get, "players/" + cid, null, ct);
        }
    }

    /// <summary>The same command mix as REST, as v2 CommandRequests, with the optional repeat check.</summary>
    private async Task RunCommandV2Async(string command, SimPlayer p, CancellationToken ct)
    {
        if (p.CommandBusy) return;
        p.CommandBusy = true;
        try
        {
            var cid = p.CharacterId;
            var item = Guid.Empty;
            string name;
            byte[] args;
            switch (command)
            {
                case "additem" when itemIds.Count > 0:
                    item = itemIds[Next(itemIds.Count)];
                    (name, args) = (RavenNest.Protocol.Commands.AddItem, CommandArgs.Pack(cid, item));
                    break;
                case "craft" when itemIds.Count > 0:
                    (name, args) = (RavenNest.Protocol.Commands.Craft, CommandArgs.Pack(cid, itemIds[Next(itemIds.Count)], 1));
                    break;
                case "produce" when recipeIds.Count > 0:
                    (name, args) = (RavenNest.Protocol.Commands.Produce, CommandArgs.Pack(cid, recipeIds[Next(recipeIds.Count)], 1L));
                    break;
                case "equipall":
                    (name, args) = (RavenNest.Protocol.Commands.EquipBest, CommandArgs.Pack(cid));
                    break;
                case "highscore":
                    (name, args) = (RavenNest.Protocol.Commands.Highscore, CommandArgs.Pack(cid, "all"));
                    break;
                default:
                    return;
            }

            var id = Guid.NewGuid();
            var first = await v2!.SendCommandAsync(name, args, ct, id);
            if (first == null || o.V2RepeatShare <= 0 || NextDouble() >= o.V2RepeatShare)
            {
                return;
            }

            var (again, _) = await v2.RepeatAsync(new CommandRequest { RequestId = id, Command = name, Args = args }, first, ct);
            if (again == null || command != "additem" || first.Status != CommandStatus.Ok)
            {
                return;
            }

            // Was the item added twice? The first answer says what the stack held after the add; the server's
            // copy must still hold exactly that (this player has no other command in flight).
            var stack = first.Inventory?.Where(u => u.CharacterId == cid).SelectMany(u => u.Stacks ?? Array.Empty<InventoryStack>())
                .LastOrDefault(s => s.ItemId == item);
            if (stack == null) return;
            var r = await api.SendAsync("GET players/{id} (repeat check)", HttpMethod.Get, "players/" + cid, null, ct);
            var items = r.Ok ? r.Json().Get("inventoryItems") as JArray : null;
            if (items == null) return;
            var held = items.Where(x => x.GuidOf("id") == stack.Id).Select(x => x.Get("amount")?.Value<long>() ?? 0).FirstOrDefault();
            Interlocked.Increment(ref metrics.V2RepeatChecked);
            if (held > stack.Amount)
            {
                Interlocked.Increment(ref metrics.V2DoubleAdds);
                Log("v2: items.add repeated with the same request id: the stack holds " + held + ", the first answer said " + stack.Amount);
            }
        }
        finally
        {
            p.CommandBusy = false;
        }
    }

    // ------------------------------------------------------------------
    // the end
    // ------------------------------------------------------------------

    /// <summary>--v2-drop-every; nothing on v1.</summary>
    public Task V2ChaosLoopAsync(CancellationToken ct) => v2?.ChaosLoopAsync(ct) ?? Task.CompletedTask;

    /// <summary>v2: keeps sending until every save is stored (or --v2-drain-timeout). Returns the players left.</summary>
    public async Task<int> DrainAsync(CancellationToken ct)
    {
        if (v2 == null) return 0;
        var clock = Stopwatch.StartNew();
        var left = await v2.DrainAsync(Players, ct);
        Log(left == 0
            ? "v2: every save stored " + clock.Elapsed.TotalSeconds.ToString("0.0") + " s after the load stopped"
            : "v2: " + left + " players still had unstored saves after " + clock.Elapsed.TotalSeconds.ToString("0.0") + " s");
        return left;
    }

    public RestApi Api => api;

    public async Task EndSessionAsync(CancellationToken ct)
    {
        if (sessionTokenB64 == null) return;
        var r = await api.SendAsync("GET game/end", HttpMethod.Get, "game/end", null, ct);
        Log(r.Ok ? "session ended" : "ending the session failed: " + r.Error);
    }

    /// <summary>
    /// The game's reconnect: a new event socket signs in while the old one is still open, then the old one
    /// closes. The old server ended the stream's game loop when the old connection went, so no more events
    /// (resource drops, village updates) arrived for the rest of the session.
    /// </summary>
    public async Task EventReconnectLoopAsync(CancellationToken ct)
    {
        if (o.EventReconnectEvery <= TimeSpan.Zero || events == null) return;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(o.EventReconnectEvery, ct);
                var replacement = new EventConnection(o.EffectiveTcpHost, o.EventPort, () => sessionTokenB64!, metrics, Log);
                replacement.Start(ct);
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (!replacement.IsConnected && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(100, ct);
                }
                await Task.Delay(1000, ct);
                var old = events;
                events = replacement;
                await old.DisposeAsync();
                Interlocked.Increment(ref metrics.EventChaosReconnects);
                Log("event socket: replaced by a new connection (chaos)");
            }
        }
        catch (OperationCanceledException) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (v2 != null) await v2.DisposeAsync();
        if (events != null) await events.DisposeAsync();
        if (delta != null) await delta.DisposeAsync();
        api.Dispose();
    }
}
