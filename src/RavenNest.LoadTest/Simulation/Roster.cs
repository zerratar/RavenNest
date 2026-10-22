using Newtonsoft.Json;

namespace RavenNest.LoadTest.Simulation;

/// <summary>
/// Remembers which character each simulated player got, per streamer, so a later run can restore
/// them in one request the way the game does after a restart. Plain local JSON, no secrets.
/// </summary>
public static class Roster
{
    public static Dictionary<string, Guid> Load(string folder, string streamer)
    {
        try
        {
            var file = PathFor(folder, streamer);
            if (!File.Exists(file)) return new Dictionary<string, Guid>();
            return JsonConvert.DeserializeObject<Dictionary<string, Guid>>(File.ReadAllText(file)) ?? new Dictionary<string, Guid>();
        }
        catch
        {
            return new Dictionary<string, Guid>();
        }
    }

    public static void Save(string folder, string streamer, IEnumerable<SimPlayer> players)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var existing = Load(folder, streamer);
            foreach (var p in players)
            {
                if (p.CharacterId != Guid.Empty) existing[p.UserName] = p.CharacterId;
            }
            var file = PathFor(folder, streamer);
            var tmp = file + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(existing, Formatting.Indented));
            File.Move(tmp, file, true);
        }
        catch
        {
            // a missing roster only means the next run joins instead of restoring
        }
    }

    private static string PathFor(string folder, string streamer)
    {
        var safe = string.Concat(streamer.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_'));
        return Path.Combine(folder, safe + ".json");
    }
}
