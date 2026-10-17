using System.Data.SqlClient;
using System.Diagnostics;
using RavenNest.LoadTest.Rest;

namespace RavenNest.LoadTest.Simulation;

public sealed record SkillGap(string Skill, int SentLevel, long SentExp, int SavedLevel, double SavedExp);

public sealed record PlayerGap(string Streamer, string UserName, Guid CharacterId, IReadOnlyList<SkillGap> Gaps);

public sealed class VerifyResult
{
    public string Source { get; init; } = "";
    public int Checked { get; set; }
    public int ReadFailures { get; set; }
    public List<string> ReadFailureMessages { get; } = new();
    public List<PlayerGap> BehindAtFirstCheck { get; } = new();
    public List<PlayerGap> StillBehind { get; } = new();
    public double? CaughtUpAfterSeconds { get; set; }
    public string? Note { get; set; }
    public bool LostSaves => StillBehind.Count > 0;
}

/// <summary>
/// "Were saves lost?": reads players back and compares each skill with the last level and exp this
/// tool actually wrote to the delta socket. The server only ever raises a skill (Skills.Set), so
/// anything lower than what was sent is an update the server dropped or has not applied.
/// </summary>
public static class Verifier
{
    private delegate (int Level, double Exp)? SavedSkill(int skillIndex);

    private static List<SkillGap> Compare(SimPlayer p, SavedSkill saved)
    {
        var gaps = new List<SkillGap>();
        for (var s = 0; s < SimPlayer.SkillCount; ++s)
        {
            if (p.SentLevel[s] < 0) continue;
            var v = saved(s);
            var sentLevel = p.SentLevel[s];
            var sentExp = p.SentExp[s];
            if (v == null)
            {
                gaps.Add(new SkillGap(SimPlayer.SkillNames[s], sentLevel, sentExp, -1, -1));
                continue;
            }

            var (lvl, exp) = v.Value;
            if (lvl < sentLevel || (lvl == sentLevel && exp < sentExp - 0.001))
            {
                gaps.Add(new SkillGap(SimPlayer.SkillNames[s], sentLevel, sentExp, lvl, exp));
            }
        }
        return gaps;
    }

    public static List<SimPlayer> Sample(StreamerSession session, int sample)
    {
        var candidates = session.Players.Where(p => p.Joined && p.HasSentExp).ToList();
        if (sample <= 0 || sample >= candidates.Count) return candidates;
        var rng = new Random(42);
        return candidates.OrderBy(_ => rng.Next()).Take(sample).ToList();
    }

    // ------------------------------------------------------------------
    // through the server: GET api/players/{characterId}
    // ------------------------------------------------------------------

    public static async Task<VerifyResult> ViaRestAsync(IReadOnlyList<StreamerSession> sessions, Options o, CancellationToken ct)
    {
        var result = new VerifyResult { Source = "REST GET api/players/{id} (what the server holds in memory)" };
        var targets = sessions.SelectMany(s => Sample(s, o.VerifySample).Select(p => (Session: s, Player: p))).ToList();
        result.Checked = targets.Count;

        var behind = await CheckRestAsync(targets, result, ct);
        result.BehindAtFirstCheck.AddRange(behind.Select(x => x.Gap));
        if (behind.Count == 0)
        {
            return result;
        }

        Console.WriteLine($"{behind.Count} players were behind on the first read, reading them again in {o.VerifyRecheck.TotalSeconds:0} s");
        try { await Task.Delay(o.VerifyRecheck, ct); } catch (OperationCanceledException) { }
        var again = await CheckRestAsync(behind.Select(x => x.Target).ToList(), null, ct);
        result.StillBehind.AddRange(again.Select(x => x.Gap));
        return result;
    }

    private static async Task<List<((StreamerSession Session, SimPlayer Player) Target, PlayerGap Gap)>> CheckRestAsync(
        List<(StreamerSession Session, SimPlayer Player)> targets, VerifyResult? result, CancellationToken ct)
    {
        var behind = new List<((StreamerSession, SimPlayer), PlayerGap)>();
        var gate = new SemaphoreSlim(16);
        var tasks = targets.Select(async t =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var r = await t.Session.Api.SendAsync("GET players/{id} (verify)", HttpMethod.Get, "players/" + t.Player.CharacterId, null, ct);
                var skills = r.Ok ? r.Json().Get("skills") : null;
                if (skills == null)
                {
                    if (result != null)
                    {
                        lock (result)
                        {
                            result.ReadFailures++;
                            if (result.ReadFailureMessages.Count < 10) result.ReadFailureMessages.Add(t.Player.UserName + ": " + (r.Error ?? "no skills in the answer"));
                        }
                    }
                    return;
                }

                var gaps = Compare(t.Player, s =>
                {
                    var name = SimPlayer.SkillNames[s];
                    var lvl = skills.Get(name + "Level");
                    var exp = skills.Get(name);
                    if (lvl == null || exp == null) return null;
                    return ((int)lvl, (double)exp);
                });

                if (gaps.Count > 0)
                {
                    lock (behind) behind.Add((t, new PlayerGap(t.Session.Name, t.Player.UserName, t.Player.CharacterId, gaps)));
                }
            }
            finally
            {
                gate.Release();
            }
        });
        await Task.WhenAll(tasks);
        return behind;
    }

    // ------------------------------------------------------------------
    // straight from the database (read only)
    // ------------------------------------------------------------------

    public static async Task<VerifyResult> ViaDatabaseAsync(IReadOnlyList<StreamerSession> sessions, Options o, CancellationToken ct)
    {
        var result = new VerifyResult { Source = "database [Character] + [Skills] (what the server has written)" };
        var connectionString = Environment.GetEnvironmentVariable(Options.DbEnv);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            result.Note = "skipped: set " + Options.DbEnv + " to the dev database connection string";
            return result;
        }

        var targets = sessions.SelectMany(s => Sample(s, o.VerifySample).Select(p => (Session: s, Player: p))).ToList();
        result.Checked = targets.Count;
        var byId = targets.ToDictionary(t => t.Player.CharacterId);
        var clock = Stopwatch.StartNew();
        List<PlayerGap> behind = new();
        var first = true;

        // The server writes changes every couple of seconds; poll until everything is there or time is up.
        while (true)
        {
            Dictionary<Guid, (int[] Levels, double[] Exps)> rows;
            try
            {
                rows = await ReadSkillsAsync(connectionString, byId.Keys.ToList(), ct);
            }
            catch (Exception exc) when (exc is SqlException or InvalidOperationException)
            {
                // the message of a SqlException does not carry the connection string
                result.ReadFailures++;
                result.Note = "could not read the database: " + exc.Message;
                return result;
            }

            behind = new List<PlayerGap>();
            foreach (var (id, t) in byId)
            {
                rows.TryGetValue(id, out var row);
                var gaps = Compare(t.Player, s => row.Levels == null ? null : (row.Levels[s], row.Exps[s]));
                if (gaps.Count > 0) behind.Add(new PlayerGap(t.Session.Name, t.Player.UserName, id, gaps));
            }

            if (first)
            {
                result.BehindAtFirstCheck.AddRange(behind);
                first = false;
            }

            if (behind.Count == 0)
            {
                result.CaughtUpAfterSeconds = clock.Elapsed.TotalSeconds;
                return result;
            }

            if (clock.Elapsed >= o.VerifyDbTimeout || ct.IsCancellationRequested)
            {
                result.StillBehind.AddRange(behind);
                return result;
            }

            try { await Task.Delay(TimeSpan.FromSeconds(2), ct); } catch (OperationCanceledException) { }
        }
    }

    private static async Task<Dictionary<Guid, (int[] Levels, double[] Exps)>> ReadSkillsAsync(string connectionString, List<Guid> ids, CancellationToken ct)
    {
        var rows = new Dictionary<Guid, (int[], double[])>();
        var columns = string.Join(", ", SimPlayer.SkillNames.Select(n => "s.[" + n + "], s.[" + n + "Level]"));
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        foreach (var chunk in ids.Chunk(500))
        {
            using var cmd = connection.CreateCommand();
            var names = new List<string>();
            for (var i = 0; i < chunk.Length; ++i)
            {
                names.Add("@p" + i);
                cmd.Parameters.AddWithValue("@p" + i, chunk[i]);
            }
            cmd.CommandText = "SELECT c.[Id], " + columns + " FROM [Character] c JOIN [Skills] s ON s.[Id] = c.[SkillsId] WHERE c.[Id] IN (" + string.Join(",", names) + ")";
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var levels = new int[SimPlayer.SkillCount];
                var exps = new double[SimPlayer.SkillCount];
                for (var s = 0; s < SimPlayer.SkillCount; ++s)
                {
                    exps[s] = reader.IsDBNull(1 + s * 2) ? 0 : Convert.ToDouble(reader.GetValue(1 + s * 2));
                    levels[s] = reader.IsDBNull(2 + s * 2) ? 0 : Convert.ToInt32(reader.GetValue(2 + s * 2));
                }
                rows[reader.GetGuid(0)] = (levels, exps);
            }
        }
        return rows;
    }
}
