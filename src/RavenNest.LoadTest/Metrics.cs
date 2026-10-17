using System.Collections.Concurrent;

namespace RavenNest.LoadTest;

public readonly record struct StatSnapshot(long Ok, long Failed, double P50, double P95, double P99, double Max, double Mean)
{
    public long Count => Ok + Failed;
}

/// <summary>Latencies and outcomes of one kind of request, for the whole run and since the last report.</summary>
public sealed class EndpointStats
{
    private readonly object gate = new();
    private readonly List<double> all = new();
    private readonly List<double> window = new();
    private readonly Dictionary<string, long> failures = new();
    private long ok, failed, windowOk, windowFailed;

    public void Record(double milliseconds, bool success, string? failure = null)
    {
        lock (gate)
        {
            all.Add(milliseconds);
            window.Add(milliseconds);
            if (success)
            {
                ok++;
                windowOk++;
                return;
            }

            failed++;
            windowFailed++;
            var key = Shorten(failure ?? "unknown failure");
            failures[key] = failures.TryGetValue(key, out var n) ? n + 1 : 1;
        }
    }

    public StatSnapshot TakeWindow()
    {
        lock (gate)
        {
            var s = Snapshot(window, windowOk, windowFailed);
            window.Clear();
            windowOk = 0;
            windowFailed = 0;
            return s;
        }
    }

    public StatSnapshot Total()
    {
        lock (gate)
        {
            return Snapshot(all, ok, failed);
        }
    }

    public IReadOnlyList<KeyValuePair<string, long>> Failures()
    {
        lock (gate)
        {
            return failures.OrderByDescending(x => x.Value).ToList();
        }
    }

    private static StatSnapshot Snapshot(List<double> samples, long ok, long failed)
    {
        if (samples.Count == 0)
        {
            return new StatSnapshot(ok, failed, 0, 0, 0, 0, 0);
        }

        var sorted = samples.ToArray();
        Array.Sort(sorted);
        return new StatSnapshot(ok, failed,
            Percentile(sorted, 0.50), Percentile(sorted, 0.95), Percentile(sorted, 0.99),
            sorted[^1], sorted.Average());
    }

    private static double Percentile(double[] sorted, double p)
    {
        var rank = (int)Math.Ceiling(p * sorted.Length) - 1;
        return sorted[Math.Clamp(rank, 0, sorted.Length - 1)];
    }

    private static string Shorten(string s)
    {
        s = s.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return s.Length > 180 ? s[..180] + "..." : s;
    }
}

/// <summary>Everything measured, shared by all simulated sessions.</summary>
public sealed class Metrics
{
    public readonly ConcurrentDictionary<string, EndpointStats> Endpoints = new();
    public readonly ConcurrentDictionary<string, long> EventsByType = new();
    public readonly DateTime Started = DateTime.UtcNow;

    // delta socket
    public long DeltaExpFrames, DeltaStateFrames, DeltaGameStateFrames, DeltaAuthFrames;
    public long DeltaExpEntries, DeltaSkillEntries, DeltaStateEntries;
    public long DeltaBytes, DeltaSendFailures;
    public long DeltaConnects, DeltaDrops, DeltaConnectFailures;
    public readonly EndpointStats DeltaSend = new();

    // event socket
    public long EventBatches, EventCount, EventBytes, EventDecodeFailures;
    public long EventConnects, EventDrops, EventConnectFailures;

    // players
    public long PlayersJoined, PlayersNew, PlayersJoinFailed, PlayersRestored;

    public EndpointStats Endpoint(string name) => Endpoints.GetOrAdd(name, _ => new EndpointStats());

    public void CountEvent(string type) => EventsByType.AddOrUpdate(type, 1, (_, n) => n + 1);
}
