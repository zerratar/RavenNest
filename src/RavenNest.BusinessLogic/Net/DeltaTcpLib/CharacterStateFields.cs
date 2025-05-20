// -----------------------------------------------------------------------------
// DeltaTcpLib.cs
// A reusable .NET Standard library for delta-based TCP messaging
// -----------------------------------------------------------------------------
using System;

namespace RavenNest.BusinessLogic.Net.DeltaTcpLib
{
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
        Position = 1 << 8,    // X, Y, Z grouped together
        AutoJoinRaid = 1 << 9,   // Counter and Count grouped
        AutoJoinDungeon = 1 << 10, // Counter and Count grouped
        IsAutoResting = 1 << 11,
        AutoTrainLevel = 1 << 12,
        AutoRestTarget = 1 << 13,
        AutoRestStart = 1 << 14,
        DungeonStyle = 1 << 15,
        RaidStyle = 1 << 16,

        Platform = 1 << 17,
        PlatformUserId = 1 << 18,
        PlatformUserName = 1 << 19
    }
}
