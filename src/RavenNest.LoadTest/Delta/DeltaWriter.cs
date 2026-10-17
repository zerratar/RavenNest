using System.Buffers.Binary;
using System.Text;
using RavenNest.Models;
using RavenNest.Models.TcpApi;

namespace RavenNest.LoadTest.Delta;

// A port of the Unity client's writer, Ravenfall Legacy
// Assets/Scripts/Shinobytes/DeltaTcpLib/DeltaClient.cs, without the Unity parts. The byte layout
// must stay exactly the same as the game's: the point is to load the server the way the game does.
// The self-test command parses what this writes with the server's own DeltaServer parsers.

public struct SkillDelta
{
    public byte Index;
    public long Experience;
    public short Level;
}

public struct DeltaExperienceUpdate
{
    public Guid CharacterId;
    public uint DirtyMask;
    public SkillDelta[] Changes;
}

public struct CharacterStateDelta
{
    public Guid CharacterId;
    public uint DirtyMask;

    public short Health;
    public Island Island;
    public Island Destination;
    public CharacterFlags State;
    public int TrainingSkillIndex;
    public string? TaskArgument;
    public long ExpPerHour;
    public DateTime EstimatedTimeForLevelUp;
    public short X, Y, Z;
    public int AutoJoinRaidCounter;
    public int AutoJoinDungeonCounter;
    public long AutoJoinRaidCount;
    public long AutoJoinDungeonCount;
    public bool IsAutoResting;
    public int AutoTrainTargetLevel;
    public double? AutoRestTarget;
    public double? AutoRestStart;
    public int? DungeonCombatStyle;
    public int? RaidCombatStyle;

    public string? PlatformUserId;
    public string? PlatformUserName;
}

public sealed class GameStateFrame
{
    public int PlayerCount;
    public DungeonState Dungeon = new();
    public RaidState Raid = new();
}

[Flags]
public enum CharacterStateFields : uint
{
    Health = 1 << 0,
    Island = 1 << 1,
    Destination = 1 << 2,
    State = 1 << 3,
    TrainingSkill = 1 << 4,
    TaskArgument = 1 << 5,
    ExpPerHour = 1 << 6,
    LevelUpETA = 1 << 7,
    Position = 1 << 8,
    AutoJoinRaid = 1 << 9,
    AutoJoinDungeon = 1 << 10,
    IsAutoResting = 1 << 11,
    AutoTrainLevel = 1 << 12,
    AutoRestTarget = 1 << 13,
    AutoRestStart = 1 << 14,
    DungeonStyle = 1 << 15,
    RaidStyle = 1 << 16,
    Platform = 1 << 17,
}

public static class DeltaFrameType
{
    public const byte Auth = 1;
    public const byte Experience = 2;
    public const byte PlayerState = 3;
    public const byte GameState = 4;
}

public static class DeltaWriter
{
    public const int MaxPlayerStateSize = 512;
    public const int MaxExpUpdateSize = 128;
    public const int MaxGameStateSize = 512;

    public static int GetPlayerStateBufferSize(int playerCount) => playerCount * MaxPlayerStateSize + 32;

    // The game sizes this at 128 bytes per player, which 17 skills at full width can pass. Sized
    // here for the worst case so a large batch never overruns; the bytes written are the same.
    public static int GetExpBufferSize(int playerCount, int maxSkills = 32) => playerCount * (16 + 4 + 10 + maxSkills * 14) + 16;

    public static int WriteVarUInt(Span<byte> buf, ulong v)
    {
        int i = 0;
        while (v >= 0x80)
        {
            buf[i++] = (byte)(v | 0x80);
            v >>= 7;
        }
        buf[i++] = (byte)v;
        return i;
    }

    private static int Write(Span<byte> span, Guid g)
    {
        g.ToByteArray().CopyTo(span);
        return 16;
    }

    private static int Write(Span<byte> span, uint v)
    {
        BinaryPrimitives.WriteUInt32BigEndian(span, v);
        return 4;
    }

    private static int Write(Span<byte> span, short v)
    {
        BinaryPrimitives.WriteInt16BigEndian(span, v);
        return 2;
    }

    private static int Write(Span<byte> span, bool v)
    {
        span[0] = (byte)(v ? 1 : 0);
        return 1;
    }

    private static int WriteShortString(Span<byte> span, string? s)
    {
        if (string.IsNullOrEmpty(s))
        {
            span[0] = 0;
            return 1;
        }

        var b = Encoding.UTF8.GetBytes(s);
        if (b.Length > 255)
        {
            throw new ArgumentException("String is too long for short string format.");
        }
        span[0] = (byte)b.Length;
        b.CopyTo(span.Slice(1));
        return 1 + b.Length;
    }

    private static int Write(Span<byte> span, DateTime dt) => WriteVarUInt(span, (ulong)dt.Ticks);

    private static int Write(Span<byte> span, Island i)
    {
        span[0] = (byte)i;
        return 1;
    }

    private static int Write(Span<byte> span, CharacterFlags f)
    {
        BinaryPrimitives.WriteInt32BigEndian(span, (int)f);
        return 4;
    }

    public static int Serialize(DeltaExperienceUpdate[] batch, byte[] output, int count)
    {
        int pos = 0;
        pos += WriteVarUInt(output.AsSpan(pos), (ulong)count);

        for (int i = 0; i < count; i++)
        {
            var d = batch[i];
            pos += Write(output.AsSpan(pos), d.CharacterId);
            pos += Write(output.AsSpan(pos), d.DirtyMask);
            pos += WriteVarUInt(output.AsSpan(pos), (ulong)d.Changes.Length);

            for (int j = 0; j < d.Changes.Length; j++)
            {
                var c = d.Changes[j];
                output[pos++] = c.Index;
                pos += WriteVarUInt(output.AsSpan(pos), (ulong)c.Experience);
                pos += WriteVarUInt(output.AsSpan(pos), (ulong)c.Level);
            }
        }

        return pos;
    }

    public static int Serialize(CharacterStateDelta[] states, byte[] output, int count)
    {
        int pos = 0;
        pos += WriteVarUInt(output.AsSpan(pos), (ulong)count);

        for (int i = 0; i < count; i++)
        {
            var d = states[i];
            pos += Write(output.AsSpan(pos), d.CharacterId);
            pos += Write(output.AsSpan(pos), d.DirtyMask);

            if ((d.DirtyMask & (uint)CharacterStateFields.Health) != 0)
                pos += Write(output.AsSpan(pos), d.Health);
            if ((d.DirtyMask & (uint)CharacterStateFields.Island) != 0)
                pos += Write(output.AsSpan(pos), d.Island);
            if ((d.DirtyMask & (uint)CharacterStateFields.Destination) != 0)
                pos += Write(output.AsSpan(pos), d.Destination);
            if ((d.DirtyMask & (uint)CharacterStateFields.State) != 0)
                pos += Write(output.AsSpan(pos), d.State);
            if ((d.DirtyMask & (uint)CharacterStateFields.TrainingSkill) != 0)
                pos += WriteVarUInt(output.AsSpan(pos), (ulong)d.TrainingSkillIndex);
            if ((d.DirtyMask & (uint)CharacterStateFields.TaskArgument) != 0)
                pos += WriteShortString(output.AsSpan(pos), d.TaskArgument);
            if ((d.DirtyMask & (uint)CharacterStateFields.ExpPerHour) != 0)
                pos += WriteVarUInt(output.AsSpan(pos), (ulong)d.ExpPerHour);
            if ((d.DirtyMask & (uint)CharacterStateFields.LevelUpETA) != 0)
                pos += Write(output.AsSpan(pos), d.EstimatedTimeForLevelUp);
            if ((d.DirtyMask & (uint)CharacterStateFields.Position) != 0)
            {
                pos += Write(output.AsSpan(pos), d.X);
                pos += Write(output.AsSpan(pos), d.Y);
                pos += Write(output.AsSpan(pos), d.Z);
            }
            if ((d.DirtyMask & (uint)CharacterStateFields.AutoJoinRaid) != 0)
            {
                pos += WriteVarUInt(output.AsSpan(pos), (ulong)d.AutoJoinRaidCounter);
                pos += WriteVarUInt(output.AsSpan(pos), (ulong)d.AutoJoinRaidCount);
            }
            if ((d.DirtyMask & (uint)CharacterStateFields.AutoJoinDungeon) != 0)
            {
                pos += WriteVarUInt(output.AsSpan(pos), (ulong)d.AutoJoinDungeonCounter);
                pos += WriteVarUInt(output.AsSpan(pos), (ulong)d.AutoJoinDungeonCount);
            }
            if ((d.DirtyMask & (uint)CharacterStateFields.IsAutoResting) != 0)
                pos += Write(output.AsSpan(pos), d.IsAutoResting);
            if ((d.DirtyMask & (uint)CharacterStateFields.AutoTrainLevel) != 0)
                pos += WriteVarUInt(output.AsSpan(pos), (ulong)d.AutoTrainTargetLevel);
            if ((d.DirtyMask & (uint)CharacterStateFields.AutoRestTarget) != 0)
            {
                output[pos++] = d.AutoRestTarget.HasValue ? (byte)1 : (byte)0;
                if (d.AutoRestTarget.HasValue)
                {
                    BinaryPrimitives.WriteInt64BigEndian(output.AsSpan(pos), BitConverter.DoubleToInt64Bits(d.AutoRestTarget.Value));
                    pos += 8;
                }
            }
            if ((d.DirtyMask & (uint)CharacterStateFields.AutoRestStart) != 0)
            {
                output[pos++] = d.AutoRestStart.HasValue ? (byte)1 : (byte)0;
                if (d.AutoRestStart.HasValue)
                {
                    BinaryPrimitives.WriteInt64BigEndian(output.AsSpan(pos), BitConverter.DoubleToInt64Bits(d.AutoRestStart.Value));
                    pos += 8;
                }
            }
            if ((d.DirtyMask & (uint)CharacterStateFields.DungeonStyle) != 0)
            {
                output[pos++] = d.DungeonCombatStyle.HasValue ? (byte)1 : (byte)0;
                if (d.DungeonCombatStyle.HasValue)
                    pos += WriteVarUInt(output.AsSpan(pos), (ulong)d.DungeonCombatStyle.Value);
            }
            if ((d.DirtyMask & (uint)CharacterStateFields.RaidStyle) != 0)
            {
                output[pos++] = d.RaidCombatStyle.HasValue ? (byte)1 : (byte)0;
                if (d.RaidCombatStyle.HasValue)
                    pos += WriteVarUInt(output.AsSpan(pos), (ulong)d.RaidCombatStyle.Value);
            }
            if ((d.DirtyMask & (uint)CharacterStateFields.Platform) != 0)
            {
                pos += WriteShortString(output.AsSpan(pos), d.PlatformUserId);
                pos += WriteShortString(output.AsSpan(pos), d.PlatformUserName);
            }
        }

        return pos;
    }

    public static int Serialize(GameStateFrame gs, byte[] output)
    {
        int pos = 0;
        pos += WriteVarUInt(output.AsSpan(pos), (ulong)gs.PlayerCount);

        output[pos++] = gs.Raid.IsActive ? (byte)1 : (byte)0;
        if (gs.Raid.IsActive)
        {
            pos += WriteVarUInt(output.AsSpan(pos), (ulong)gs.Raid.BossCombatLevel);
            pos += WriteVarUInt(output.AsSpan(pos), (ulong)gs.Raid.CurrentBossHealth);
            pos += WriteVarUInt(output.AsSpan(pos), (ulong)gs.Raid.MaxBossHealth);
            pos += WriteVarUInt(output.AsSpan(pos), (ulong)gs.Raid.PlayersJoined);
            pos += Write(output.AsSpan(pos), gs.Raid.EndTime);
        }
        pos += Write(output.AsSpan(pos), gs.Raid.NextRaid);

        output[pos++] = gs.Dungeon.IsActive ? (byte)1 : (byte)0;
        if (gs.Dungeon.IsActive)
        {
            if (!string.IsNullOrEmpty(gs.Dungeon.Name))
            {
                var dungeonName = Encoding.UTF8.GetBytes(gs.Dungeon.Name);
                pos += WriteVarUInt(output.AsSpan(pos), (ulong)dungeonName.Length);
                dungeonName.CopyTo(output.AsSpan(pos));
                pos += dungeonName.Length;
            }
            else
            {
                pos += WriteVarUInt(output.AsSpan(pos), 0);
            }
            output[pos++] = gs.Dungeon.HasStarted ? (byte)1 : (byte)0;
            pos += WriteVarUInt(output.AsSpan(pos), (ulong)gs.Dungeon.BossCombatLevel);
            pos += WriteVarUInt(output.AsSpan(pos), (ulong)gs.Dungeon.CurrentBossHealth);
            pos += WriteVarUInt(output.AsSpan(pos), (ulong)gs.Dungeon.MaxBossHealth);
            pos += WriteVarUInt(output.AsSpan(pos), (ulong)gs.Dungeon.PlayersAlive);
            pos += WriteVarUInt(output.AsSpan(pos), (ulong)gs.Dungeon.PlayersJoined);
            pos += WriteVarUInt(output.AsSpan(pos), (ulong)gs.Dungeon.EnemiesLeft);
            pos += Write(output.AsSpan(pos), gs.Dungeon.StartTime);
        }
        pos += Write(output.AsSpan(pos), gs.Dungeon.NextDungeon);
        return pos;
    }

    /// <summary>[length:4 big endian][type:1][payload], length counting the type byte, as DeltaClient.SendFrameAsync.</summary>
    public static byte[] Frame(byte type, ReadOnlySpan<byte> payload)
    {
        var frame = new byte[payload.Length + 5];
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(0, 4), 1 + payload.Length);
        frame[4] = type;
        payload.CopyTo(frame.AsSpan(5));
        return frame;
    }
}
