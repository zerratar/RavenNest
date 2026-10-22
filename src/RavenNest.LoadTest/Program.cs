using RavenNest.LoadTest;
using RavenNest.LoadTest.Simulation;

Options options;
try
{
    options = Options.Parse(args);
}
catch (Exception exc) when (exc is ArgumentException or FormatException or UriFormatException)
{
    Console.Error.WriteLine(exc.Message);
    return 1;
}

switch (options.Command)
{
    case "help":
        Console.WriteLine(Options.Help);
        return 0;
    case "self-test":
        return SelfTest.Run(options);
    case "seed":
        return await Seed.RunAsync(options);
    case "run":
        return await RunAsync(options);
    default:
        Console.Error.WriteLine("Unknown command '" + options.Command + "'. Use run, seed, self-test or help.");
        return 1;
}

static async Task<int> RunAsync(Options o)
{
    var refusal = Safety.Check(o);
    if (refusal != null)
    {
        Console.Error.WriteLine(refusal);
        return 1;
    }

    var usingToken = !string.IsNullOrEmpty(o.AuthToken) && o.Sessions == 1;
    if (!usingToken && string.IsNullOrEmpty(o.Password))
    {
        Console.Error.WriteLine("No password for the test streamer. Set " + Options.PasswordEnv + " (see README: the seed step creates the account).");
        return 1;
    }
    if (string.IsNullOrEmpty(o.AccessKey))
    {
        Console.Error.WriteLine("No access key. Set " + Options.AccessKeyEnv + " to the game's access key (the one the Unity GameManager uses).");
        return 1;
    }

    Console.WriteLine(o.Protocol == WireProtocol.V2
        ? $"RavenNest load test: {o.Sessions} session(s) x {o.Players} players against {o.BaseUrl} on the v2 connection{(o.V2Commands ? " (joins and commands too)" : "")}, {o.Duration.TotalSeconds:0} s"
        : $"RavenNest load test: {o.Sessions} session(s) x {o.Players} players against {o.BaseUrl} (sockets {o.EffectiveTcpHost}:{o.EventPort}/{o.DeltaPort}), {o.Duration.TotalSeconds:0} s");
    Console.WriteLine("Ctrl+C stops early and still verifies; press it twice to abort.");

    var metrics = new Metrics();
    var sessions = Enumerable.Range(0, o.Sessions).Select(i => new StreamerSession(i, o, metrics)).ToList();
    var report = new Report(metrics, sessions, o);

    using var abort = new CancellationTokenSource();   // everything, second Ctrl+C
    using var run = CancellationTokenSource.CreateLinkedTokenSource(abort.Token); // the load phase
    var cancelPresses = 0;
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        if (Interlocked.Increment(ref cancelPresses) == 1)
        {
            Console.WriteLine("stopping the load, then verifying...");
            try { run.Cancel(); } catch { }
        }
        else
        {
            Console.WriteLine("aborting");
            try { abort.Cancel(); } catch { }
        }
    };

    var exitCode = 0;
    VerifyResult? rest = null, db = null;
    try
    {
        var started = await Task.WhenAll(sessions.Select(s => s.StartAsync(run.Token)));
        var live = sessions.Where((s, i) => started[i]).ToList();
        if (live.Count == 0)
        {
            Console.Error.WriteLine("No session could be started; nothing to test.");
            return 1;
        }

        run.CancelAfter(o.Duration);

        var reporter = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(o.ReportInterval);
            try
            {
                while (await timer.WaitForNextTickAsync(run.Token)) report.PrintProgress();
            }
            catch (OperationCanceledException) { }
        });

        var work = live.SelectMany(s => new[]
        {
            s.JoinPlayersAsync(run.Token),
            s.TickLoopAsync(run.Token),
            s.CommandLoopAsync(run.Token),
            s.EventReconnectLoopAsync(run.Token),
            s.V2ChaosLoopAsync(run.Token)
        }).ToList();

        await Task.WhenAll(work);
        await reporter;

        if (abort.IsCancellationRequested)
        {
            report.PrintFinal(null, null);
            return 1;
        }

        if (o.Protocol == WireProtocol.V2)
        {
            // the game's quit: send what is not stored yet and wait for the Stored acks
            Console.WriteLine($"load stopped; waiting up to {o.V2DrainTimeout.TotalSeconds:0} s for every v2 save to be stored");
            await Task.WhenAll(live.Select(s => s.DrainAsync(abort.Token)));
        }

        if (o.Verify)
        {
            Console.WriteLine($"load stopped; verifying in {o.VerifyDelay.TotalSeconds:0} s");
            try { await Task.Delay(o.VerifyDelay, abort.Token); } catch (OperationCanceledException) { }
            rest = await Verifier.ViaRestAsync(live, o, abort.Token);
            if (o.VerifyDb)
            {
                db = await Verifier.ViaDatabaseAsync(live, o, abort.Token);
            }
        }

        if (o.EndSession)
        {
            await Task.WhenAll(live.Select(s => s.EndSessionAsync(CancellationToken.None)));
        }
    }
    catch (OperationCanceledException) when (abort.IsCancellationRequested)
    {
        exitCode = 1;
    }
    finally
    {
        foreach (var s in sessions)
        {
            await s.DisposeAsync();
        }
    }

    report.PrintFinal(rest, db);
    if (o.JsonReport != null)
    {
        report.WriteJson(o.JsonReport, rest, db);
    }

    if ((rest?.LostSaves ?? false) || (db?.LostSaves ?? false))
    {
        return 2;
    }
    return exitCode;
}
