using RavenNest.DataAnnotations;
using System;
using System.Collections.Generic;

namespace RavenNest.DataModels
{
    public partial class CharacterState : Entity<CharacterState>
    {
        [PersistentData] private int health;
        [PersistentData] private bool inRaid;
        [PersistentData] private bool inArena;
        [PersistentData] private bool? inDungeon;
        [PersistentData] private bool? inOnsen;
        [PersistentData] private bool? joinedDungeon;
        [PersistentData] private string task;
        [PersistentData] private string taskArgument;
        [PersistentData] private string island;
        [PersistentData] private string destination;
        [PersistentData] private string estimatedTimeForLevelUp;
        [PersistentData] private long? expPerHour;
        [PersistentData] private double? x;
        [PersistentData] private double? y;
        [PersistentData] private double? z;
        [PersistentData] private double? restedTime;
        [PersistentData] private bool? isCaptain;

        [PersistentData] private int autoJoinDungeonCounter;
        [PersistentData] private int autoJoinRaidCounter;

        [PersistentData] private long autoJoinDungeonCount;

        ///// <summary>
        /////     How many times the player has auto joined the dungeon, not to be confused with the <see cref="AutoJoinDungeonCounter"/> which is maximum amount of times to join.
        ///// </summary>
        //public long AutoJoinDungeonCount { get => autoJoinDungeonCount; set => Set(ref autoJoinDungeonCount, value); }
        [PersistentData] private long autoJoinRaidCount;
        ///// <summary>
        /////     How many times the player has auto joined the raid, not to be confused with the <see cref="AutoJoinRaidCounter"/> which is maximum amount of times to join.
        ///// </summary>
        //public long AutoJoinRaidCount { get => autoJoinRaidCount; set => Set(ref autoJoinRaidCount, value); }

        [PersistentData] private bool isAutoResting;
        ///// <summary>
        /////     How many times the player has auto rested.
        ///// </summary>
        //public bool IsAutoResting { get => isAutoResting; set => Set(ref isAutoResting, value); }

        [PersistentData] private int autoTrainTargetLevel;
        [PersistentData] private double? autoRestTarget;
        [PersistentData] private double? autoRestStart;
        [PersistentData] private int? dungeonCombatStyle;
        [PersistentData] private int? raidCombatStyle;
    }
}
