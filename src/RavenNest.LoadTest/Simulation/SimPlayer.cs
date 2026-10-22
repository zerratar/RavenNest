using Newtonsoft.Json.Linq;
using RavenNest.BusinessLogic;
using RavenNest.LoadTest.Delta;
using RavenNest.LoadTest.Rest;
using RavenNest.Models;

namespace RavenNest.LoadTest.Simulation;

/// <summary>One simulated viewer: who they are, their skills as the "game" sees them, and what was last written to the server.</summary>
public sealed class SimPlayer
{
    public static readonly string[] SkillNames = RavenNest.DataModels.Skills.SkillNames;
    public static readonly int SkillCount = SkillNames.Length;

    private static readonly int[] CombatSkills = { (int)Skill.Attack, (int)Skill.Strength, (int)Skill.Defense, (int)Skill.Magic, (int)Skill.Ranged, (int)Skill.Healing };
    private static readonly int[] GatherSkills = { (int)Skill.Woodcutting, (int)Skill.Fishing, (int)Skill.Mining, (int)Skill.Crafting, (int)Skill.Cooking, (int)Skill.Farming, (int)Skill.Gathering, (int)Skill.Alchemy };

    public SimPlayer(int index, string userName, string platformId)
    {
        Index = index;
        UserName = userName;
        PlatformId = platformId;
        for (var i = 0; i < SkillCount; ++i)
        {
            Level[i] = 1;
            SentLevel[i] = -1;
        }
    }

    public int Index { get; }
    public string UserName { get; }
    public string PlatformId { get; }

    public Guid CharacterId { get; set; }
    public Guid UserId { get; set; }
    public volatile bool Joined;
    public bool IsNewUser { get; set; }

    // the "game" side
    public readonly int[] Level = new int[32];
    public readonly double[] Exp = new double[32];
    public uint ExpDirty;

    // last values written to the delta socket, per skill
    public readonly int[] SentLevel = new int[32];
    public readonly long[] SentExp = new long[32];

    // training and state
    public int TrainingSkill = -1;
    public int SecondarySkill = -1;
    public string TaskArgument = "";
    public Island Island = Island.Home;
    public short X, Y, Z;
    public short Health = 10;
    public long ExpPerHour;
    public DateTime LevelUpEta = DateTime.MaxValue;
    public CharacterStateDelta? LastSentState;

    // ---- v2: the outbox, what the game client keeps per player until the server says it is stored ----

    /// <summary>Goes up whenever something to save changed (a tick with exp or state changes, a full resync).</summary>
    public long Version;
    public long AcceptedVersion;
    public long StoredVersion;

    /// <summary>The next version carries every skill and state field (after joining, or a ResyncRequired).</summary>
    public bool NeedsFull = true;

    /// <summary>The versions not stored yet, with what each changed; a resend carries all of their changes.</summary>
    public readonly List<PendingSave> Pending = new();

    /// <summary>The newest version that went out at least once, to count resends.</summary>
    public long LastSentVersion;

    /// <summary>When the player's save last went out; an unstored save goes again after --v2-resend-after.</summary>
    public DateTime LastSentAt = DateTime.MinValue;

    /// <summary>Exp gained per skill since the last version was made (SkillSave.Gained).</summary>
    public readonly double[] GainedSinceVersion = new double[32];

    /// <summary>A v2 command for this player is in flight, so a repeat check can read the inventory without a race.</summary>
    public volatile bool CommandBusy;

    public bool HasUnstoredSave => Version > StoredVersion;

    public bool HasSentExp
    {
        get
        {
            for (var i = 0; i < SkillCount; ++i) if (SentLevel[i] >= 0) return true;
            return false;
        }
    }

    /// <summary>Takes the skills the server returned on join, so the run continues from what is saved.</summary>
    public void LoadSkills(JToken? skills)
    {
        if (skills == null) return;
        for (var i = 0; i < SkillCount; ++i)
        {
            var name = SkillNames[i];
            var lvl = skills.Get(name + "Level")?.Value<int>() ?? 1;
            var exp = skills.Get(name)?.Value<double>() ?? 0;
            Level[i] = Math.Max(1, lvl);
            Exp[i] = Math.Max(0, exp);
        }
        Health = (short)Math.Clamp(Level[(int)Skill.Health], 1, short.MaxValue);
    }

    public void PickTraining(Random rng)
    {
        if (rng.NextDouble() < 0.5)
        {
            // fighting trains the chosen combat skill and health together
            TrainingSkill = CombatSkills[rng.Next(CombatSkills.Length)];
            SecondarySkill = (int)Skill.Health;
        }
        else
        {
            TrainingSkill = GatherSkills[rng.Next(GatherSkills.Length)];
            SecondarySkill = -1;
        }
        // the server derives the task from the skill index and keeps the argument for fighting ("Attack", "Magic", ...)
        TaskArgument = SkillNames[TrainingSkill];
    }

    /// <summary>Gains exp the way SkillStat.AddExp does: exp is within the level, a level up carries the rest over.</summary>
    public void GainExp(int skill, double amount)
    {
        if (amount <= 0) return;
        Exp[skill] += amount;
        GainedSinceVersion[skill] += amount;
        var next = GameMath.ExperienceForLevel(Level[skill] + 1);
        while (Exp[skill] >= next && Level[skill] < GameMath.MaxLevel)
        {
            Exp[skill] -= next;
            Level[skill]++;
            next = GameMath.ExperienceForLevel(Level[skill] + 1);
        }
        ExpDirty |= 1u << skill;
    }

    public CharacterStateDelta CurrentState() => new CharacterStateDelta
    {
        CharacterId = CharacterId,
        Health = Health,
        Island = Island,
        Destination = Island.None,
        State = CharacterFlags.None,
        TrainingSkillIndex = TrainingSkill,
        TaskArgument = TaskArgument,
        ExpPerHour = ExpPerHour,
        EstimatedTimeForLevelUp = LevelUpEta,
        X = X,
        Y = Y,
        Z = Z,
        AutoTrainTargetLevel = 0,
        AutoRestTarget = null,
        AutoRestStart = null,
        DungeonCombatStyle = null,
        RaidCombatStyle = null,
        PlatformUserId = PlatformId,
        PlatformUserName = UserName,
    };

    /// <summary>The same field comparison as DeltaClientBehaviour.BuildStateUpdateBatch; everything when nothing was sent yet.</summary>
    public uint StateDirtyMask(in CharacterStateDelta current)
    {
        if (LastSentState is not { } last)
        {
            return 0xFFFFFFFF;
        }

        uint m = 0;
        if (current.Health != last.Health) m |= (uint)CharacterStateFields.Health;
        if (current.Island != last.Island) m |= (uint)CharacterStateFields.Island;
        if (current.Destination != last.Destination) m |= (uint)CharacterStateFields.Destination;
        if (current.State != last.State) m |= (uint)CharacterStateFields.State;
        if (current.TrainingSkillIndex != last.TrainingSkillIndex || current.TaskArgument != last.TaskArgument)
            m |= (uint)(CharacterStateFields.TrainingSkill | CharacterStateFields.TaskArgument);
        if (current.ExpPerHour != last.ExpPerHour) m |= (uint)CharacterStateFields.ExpPerHour;
        if (current.EstimatedTimeForLevelUp != last.EstimatedTimeForLevelUp) m |= (uint)CharacterStateFields.LevelUpETA;
        if (current.X != last.X || current.Y != last.Y || current.Z != last.Z) m |= (uint)CharacterStateFields.Position;
        if (current.AutoJoinRaidCounter != last.AutoJoinRaidCounter || current.AutoJoinRaidCount != last.AutoJoinRaidCount)
            m |= (uint)CharacterStateFields.AutoJoinRaid;
        if (current.AutoJoinDungeonCounter != last.AutoJoinDungeonCounter || current.AutoJoinDungeonCount != last.AutoJoinDungeonCount)
            m |= (uint)CharacterStateFields.AutoJoinDungeon;
        if (current.IsAutoResting != last.IsAutoResting) m |= (uint)CharacterStateFields.IsAutoResting;
        if (current.AutoTrainTargetLevel != last.AutoTrainTargetLevel) m |= (uint)CharacterStateFields.AutoTrainLevel;
        if (current.AutoRestTarget != last.AutoRestTarget) m |= (uint)CharacterStateFields.AutoRestTarget;
        if (current.AutoRestStart != last.AutoRestStart) m |= (uint)CharacterStateFields.AutoRestStart;
        if (current.DungeonCombatStyle != last.DungeonCombatStyle) m |= (uint)CharacterStateFields.DungeonStyle;
        if (current.RaidCombatStyle != last.RaidCombatStyle) m |= (uint)CharacterStateFields.RaidStyle;
        if ((current.PlatformUserId != last.PlatformUserId || current.PlatformUserName != last.PlatformUserName)
            && !string.IsNullOrEmpty(current.PlatformUserName) && !string.IsNullOrEmpty(current.PlatformUserId))
            m |= (uint)CharacterStateFields.Platform;
        return m;
    }
}

/// <summary>One v2 player version that is not stored yet.</summary>
public sealed class PendingSave
{
    public long Version;
    public uint SkillMask;
    public uint StateMask;
    public bool Full;
    public double[]? Gained;

    /// <summary>Stopwatch timestamp of the first send, 0 until sent.</summary>
    public long FirstSent;
}
