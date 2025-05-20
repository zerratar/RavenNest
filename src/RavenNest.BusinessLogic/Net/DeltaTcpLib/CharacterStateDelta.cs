// -----------------------------------------------------------------------------
// DeltaTcpLib.cs
// A reusable .NET Standard library for delta-based TCP messaging
// -----------------------------------------------------------------------------
using System;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Net.DeltaTcpLib
{
    public struct CharacterStateDelta
    {
        public Guid CharacterId;
        public uint DirtyMask;

        public short Health;
        public Island Island;
        public Island Destination;
        public CharacterFlags State;
        public int TrainingSkillIndex;
        public string TaskArgument;
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

        public string? Platform;
        public string? PlatformUserId;
        public string? PlatformUserName;

        public bool HasValue(CharacterStateFields field) =>
            (DirtyMask & (uint)field) != 0;
    }
}
