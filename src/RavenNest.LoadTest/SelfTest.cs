using System.Buffers.Binary;
using System.Text;
using MessagePack;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Net.DeltaTcpLib;
using RavenNest.LoadTest.Delta;
using RavenNest.LoadTest.Events;
using RavenNest.LoadTest.Rest;
using RavenNest.LoadTest.Simulation;
using RavenNest.Models;
using RavenNest.Models.TcpApi;
using Server = RavenNest.BusinessLogic.Net.DeltaTcpLib;
using Tool = RavenNest.LoadTest.Delta;

namespace RavenNest.LoadTest;

/// <summary>
/// Writes frames the way the tool sends them and reads them back with the server's own parsers
/// (DeltaServer, SessionManager's token decoding, TcpSocketApi's MessagePack envelope). No network.
/// </summary>
public static class SelfTest
{
    private static int failures;

    public static int Run(Options o)
    {
        failures = 0;
        var rng = new Random(7);

        ExperienceRoundTrip(rng);
        StateRoundTrip(rng, o.ClientVersion);
        GameStateRoundTrip();
        FrameHeader();
        TokenRoundTrip();
        EventAuthPacket();
        SafetyChecks();

        Console.WriteLine(failures == 0 ? "self-test: all checks passed" : "self-test: " + failures + " check(s) FAILED");
        return failures == 0 ? 0 : 1;
    }

    private static void Check(bool ok, string what)
    {
        if (ok) return;
        failures++;
        Console.WriteLine("FAIL: " + what);
    }

    private static void ExperienceRoundTrip(Random rng)
    {
        var updates = new Tool.DeltaExperienceUpdate[300];
        for (var i = 0; i < updates.Length; ++i)
        {
            var count = 1 + rng.Next(3) + (i == 0 ? SimPlayer.SkillCount - 1 : 0);
            var changes = Enumerable.Range(0, Math.Min(count, SimPlayer.SkillCount))
                .Select(s => new Tool.SkillDelta { Index = (byte)s, Experience = i == 1 ? long.MaxValue / 4 : rng.NextInt64(0, 50_000_000_000), Level = (short)rng.Next(1, 1000) })
                .ToArray();
            updates[i] = new Tool.DeltaExperienceUpdate { CharacterId = Guid.NewGuid(), DirtyMask = (uint)rng.Next(1, int.MaxValue), Changes = changes };
        }

        var buffer = new byte[DeltaWriter.GetExpBufferSize(updates.Length)];
        var length = DeltaWriter.Serialize(updates, buffer, updates.Length);
        var pos = 0;
        var parsed = DeltaServer.ParseExperience(buffer.AsSpan(0, length), ref pos);
        Check(pos == length, "experience: the server read " + pos + " of " + length + " bytes");
        Check(parsed.Count == updates.Length, "experience: count");
        for (var i = 0; i < Math.Min(parsed.Count, updates.Length); ++i)
        {
            var a = updates[i];
            var b = parsed[i];
            Check(a.CharacterId == b.CharacterId && a.DirtyMask == b.DirtyMask && a.Changes.Length == b.Changes.Length, "experience: entry " + i);
            for (var j = 0; j < Math.Min(a.Changes.Length, b.Changes.Length); ++j)
            {
                Check(a.Changes[j].Index == b.Changes[j].Index && a.Changes[j].Experience == b.Changes[j].Experience && a.Changes[j].Level == b.Changes[j].Level,
                    "experience: entry " + i + " skill " + j);
            }
        }
    }

    private static void StateRoundTrip(Random rng, string clientVersion)
    {
        var players = Enumerable.Range(1, 200).Select(n => new SimPlayer(n, "loadtest_s1_" + n.ToString("D5"), "loadtest-s1-" + n.ToString("D5"))).ToList();
        var states = new List<Tool.CharacterStateDelta>();
        foreach (var p in players)
        {
            p.CharacterId = Guid.NewGuid();
            p.PickTraining(rng);
            p.X = (short)rng.Next(-500, 500);
            p.Z = (short)rng.Next(-500, 500);
            p.Health = (short)rng.Next(1, 999);
            p.ExpPerHour = rng.NextInt64(0, 10_000_000);
            p.LevelUpEta = p.Index % 3 == 0 ? DateTime.MaxValue : DateTime.UtcNow.AddMinutes(rng.Next(1, 10000));
            var s = p.CurrentState();
            if (p.Index % 4 == 0)
            {
                s.AutoRestTarget = rng.NextDouble() * 100;
                s.AutoRestStart = null;
                s.DungeonCombatStyle = 2;
                s.RaidCombatStyle = null;
                s.TrainingSkillIndex = -1; // not training, as the game sends it
            }

            // first send: everything, as the game; later sends: a typical small mask
            s.DirtyMask = p.Index % 2 == 0 ? 0xFFFFFFFF : (uint)(Tool.CharacterStateFields.Position | Tool.CharacterStateFields.ExpPerHour | Tool.CharacterStateFields.LevelUpETA | Tool.CharacterStateFields.Health);
            states.Add(s);
        }

        var buffer = new byte[DeltaWriter.GetPlayerStateBufferSize(states.Count)];
        var length = DeltaWriter.Serialize(states.ToArray(), buffer, states.Count);
        var pos = 0;
        var parsed = DeltaServer.ParsePlayerState(buffer.AsSpan(0, length), ref pos, clientVersion);
        Check(pos == length, "state: the server read " + pos + " of " + length + " bytes");
        Check(parsed.Count == states.Count, "state: count " + parsed.Count + " of " + states.Count);
        for (var i = 0; i < Math.Min(parsed.Count, states.Count); ++i)
        {
            var a = states[i];
            var b = parsed[i];
            var m = a.DirtyMask;
            bool Has(Tool.CharacterStateFields f) => (m & (uint)f) != 0;
            var ok = a.CharacterId == b.CharacterId && a.DirtyMask == b.DirtyMask
                && (!Has(Tool.CharacterStateFields.Health) || a.Health == b.Health)
                && (!Has(Tool.CharacterStateFields.Island) || a.Island == b.Island)
                && (!Has(Tool.CharacterStateFields.Destination) || a.Destination == b.Destination)
                && (!Has(Tool.CharacterStateFields.State) || a.State == b.State)
                && (!Has(Tool.CharacterStateFields.TrainingSkill) || a.TrainingSkillIndex == b.TrainingSkillIndex)
                && (!Has(Tool.CharacterStateFields.TaskArgument) || (a.TaskArgument ?? "") == (b.TaskArgument ?? ""))
                && (!Has(Tool.CharacterStateFields.ExpPerHour) || a.ExpPerHour == b.ExpPerHour)
                && (!Has(Tool.CharacterStateFields.LevelUpETA) || a.EstimatedTimeForLevelUp.Ticks == b.EstimatedTimeForLevelUp.Ticks)
                && (!Has(Tool.CharacterStateFields.Position) || (a.X == b.X && a.Y == b.Y && a.Z == b.Z))
                && (!Has(Tool.CharacterStateFields.AutoRestTarget) || a.AutoRestTarget == b.AutoRestTarget)
                && (!Has(Tool.CharacterStateFields.AutoRestStart) || a.AutoRestStart == b.AutoRestStart)
                && (!Has(Tool.CharacterStateFields.DungeonStyle) || a.DungeonCombatStyle == b.DungeonCombatStyle)
                && (!Has(Tool.CharacterStateFields.RaidStyle) || a.RaidCombatStyle == b.RaidCombatStyle)
                && (!Has(Tool.CharacterStateFields.Platform) || (a.PlatformUserId == b.PlatformUserId && a.PlatformUserName == b.PlatformUserName && b.Platform == "twitch"));
            Check(ok, "state: entry " + i + " differs after the server parsed it");
        }
    }

    private static void GameStateRoundTrip()
    {
        var gs = new GameStateFrame { PlayerCount = 1234 };
        gs.Raid.NextRaid = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        gs.Dungeon.NextDungeon = new DateTime(2026, 10, 17, 12, 30, 0, DateTimeKind.Utc);
        var buffer = new byte[DeltaWriter.MaxGameStateSize];
        var length = DeltaWriter.Serialize(gs, buffer);
        var pos = 0;
        var parsed = DeltaServer.ParseGameState(buffer.AsSpan(0, length), ref pos);
        Check(pos == length, "game state: the server read " + pos + " of " + length + " bytes");
        Check(parsed.PlayerCount == gs.PlayerCount && !parsed.Raid.IsActive && !parsed.Dungeon.IsActive
            && parsed.Raid.NextRaid == gs.Raid.NextRaid && parsed.Dungeon.NextDungeon == gs.Dungeon.NextDungeon, "game state: fields");
    }

    private static void FrameHeader()
    {
        var payload = new byte[] { 1, 2, 3 };
        var frame = DeltaWriter.Frame(DeltaFrameType.Experience, payload);
        Check(frame.Length == 8 && BinaryPrimitives.ReadInt32BigEndian(frame) == 4 && frame[4] == DeltaFrameType.Experience && frame[5] == 1 && frame[7] == 3,
            "frame header: [len:4 BE = 1 + payload][type][payload]");
    }

    private static void TokenRoundTrip()
    {
        var token = new SessionToken
        {
            SessionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UserName = "loadtest_streamer",
            ClientVersion = "0.9.4.4a",
            StartedUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.AddDays(180),
            AuthToken = "x"
        };
        var b64 = RestApi.Encode(token);
        // what DeltaServer and SessionManager.Get do with it
        var raw = Encoding.UTF8.GetString(DeltaConnection.AuthPayload(b64));
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(raw));
        var back = RavenNest.BusinessLogic.JSON.Parse<SessionToken>(json);
        Check(back != null && back.SessionId == token.SessionId && back.UserId == token.UserId && back.ClientVersion == token.ClientVersion && !back.Expired,
            "session token: base64 JSON round trip through the server's JSON.Parse");
    }

    private static void EventAuthPacket()
    {
        var b64 = RestApi.Encode(new SessionToken { SessionId = Guid.NewGuid(), ExpiresUtc = DateTime.UtcNow.AddDays(1) });
        var bytes = EventConnection.BuildAuthPacket(b64);
        var options = MessagePack.Resolvers.ContractlessStandardResolver.Options;
        var typed = MessagePackSerializer.Deserialize<TypedPacket>(bytes, options);
        var auth = MessagePackSerializer.Deserialize<AuthenticationRequest>(typed.Payload, options);
        Check(typed.MessageType == TcpMessageType.AuthenticationRequest && typed.SessionToken == b64 && auth.SessionToken == b64
            && (DateTimeOffset.UtcNow - typed.Timestamp) < TimeSpan.FromMinutes(1), "event socket auth packet");
    }

    private static void SafetyChecks()
    {
        Check(Safety.IsLocalHost("localhost") && Safety.IsLocalHost("127.0.0.1") && Safety.IsLocalHost("::1") && Safety.IsLocalHost("[::1]"), "safety: local hosts allowed");
        Check(!Safety.IsLocalHost("www.ravenfall.stream") && !Safety.IsLocalHost("ravenfall.stream") && !Safety.IsLocalHost("10.0.0.5") && !Safety.IsLocalHost(""),
            "safety: other hosts refused");
    }
}
