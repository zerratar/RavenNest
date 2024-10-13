using System;
using System.Collections.Generic;

namespace RavenNest.DataModels
{
    public partial class CharacterState : Entity<CharacterState>
    {
        private int health; public int Health { get => health; set => Set(ref health, value); }
        private bool inRaid; public bool InRaid { get => inRaid; set => Set(ref inRaid, value); }
        private bool inArena; public bool InArena { get => inArena; set => Set(ref inArena, value); }
        private bool? inDungeon; public bool? InDungeon { get => inDungeon; set => Set(ref inDungeon, value); }
        private bool? inOnsen; public bool? InOnsen { get => inOnsen; set => Set(ref inOnsen, value); }
        private bool? joinedDungeon; public bool? JoinedDungeon { get => joinedDungeon; set => Set(ref joinedDungeon, value); }
        private string task; public string Task { get => task; set => Set(ref task, value); }
        private string taskArgument; public string TaskArgument { get => taskArgument; set => Set(ref taskArgument, value); }
        private string island; public string Island { get => island; set => Set(ref island, value); }
        private string destination; public string Destination { get => destination; set => Set(ref destination, value); }
        private string estimatedTimeForLevelUp; public string EstimatedTimeForLevelUp { get => estimatedTimeForLevelUp; set => Set(ref estimatedTimeForLevelUp, value); }
        private long? expPerHour; public long? ExpPerHour { get => expPerHour; set => Set(ref expPerHour, value); }
        private double? x; public double? X { get => x; set => Set(ref x, value); }
        private double? y; public double? Y { get => y; set => Set(ref y, value); }
        private double? z; public double? Z { get => z; set => Set(ref z, value); }
        private double? restedTime; public double? RestedTime { get => restedTime; set => Set(ref restedTime, value); }
        private bool? isCaptain; public bool? IsCaptain { get => isCaptain; set => Set(ref isCaptain, value); }

        private int autoJoinDungeonCounter; public int AutoJoinDungeonCounter { get => autoJoinDungeonCounter; set => Set(ref autoJoinDungeonCounter, value); }
        private int autoJoinRaidCounter; public int AutoJoinRaidCounter { get => autoJoinRaidCounter; set => Set(ref autoJoinRaidCounter, value); }

        private long autoJoinDungeonCount;

        /// <summary>
        ///     How many times the player has auto joined the dungeon, not to be confused with the <see cref="AutoJoinDungeonCounter"/> which is maximum amount of times to join.
        /// </summary>
        public long AutoJoinDungeonCount { get => autoJoinDungeonCount; set => Set(ref autoJoinDungeonCount, value); }
        private long autoJoinRaidCount;
        /// <summary>
        ///     How many times the player has auto joined the raid, not to be confused with the <see cref="AutoJoinRaidCounter"/> which is maximum amount of times to join.
        /// </summary>
        public long AutoJoinRaidCount { get => autoJoinRaidCount; set => Set(ref autoJoinRaidCount, value); }

        private bool isAutoResting;
        /// <summary>
        ///     How many times the player has auto rested.
        /// </summary>
        public bool IsAutoResting { get => isAutoResting; set => Set(ref isAutoResting, value); }

        private int autoTrainTargetLevel; public int AutoTrainTargetLevel { get => autoTrainTargetLevel; set => Set(ref autoTrainTargetLevel, value); }
        private double? autoRestTarget; public double? AutoRestTarget { get => autoRestTarget; set => Set(ref autoRestTarget, value); }
        private double? autoRestStart; public double? AutoRestStart { get => autoRestStart; set => Set(ref autoRestStart, value); }
        private int? dungeonCombatStyle; public int? DungeonCombatStyle { get => dungeonCombatStyle; set => Set(ref dungeonCombatStyle, value); }
        private int? raidCombatStyle; public int? RaidCombatStyle { get => raidCombatStyle; set => Set(ref raidCombatStyle, value); }
    }
}
