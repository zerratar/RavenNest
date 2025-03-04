﻿using System;

namespace RavenNest.Models
{
    public class CharacterState
    {
        public CharacterState() { }
        public Guid Id { get; set; }
        public int Health { get; set; }
        public bool InRaid { get; set; }
        public bool InArena { get; set; }
        public bool InDungeon { get; set; }
        public bool InOnsen { get; set; }
        public bool JoinedDungeon { get; set; }
        public string Task { get; set; }
        public string TaskArgument { get; set; }
        public string Island { get; set; }
        public string Destination { get; set; }
        public long? ExpPerHour { get; set; }
        public DateTime? EstimatedTimeForLevelUp { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
        public double? Z { get; set; }
        public double RestedTime { get; set; }
        public bool? IsCaptain { get; set; }
        public int AutoJoinDungeonCounter { get; set; }
        public int AutoJoinRaidCounter { get; set; }
        public int AutoTrainTargetLevel { get; set; }
        public double? AutoRestTarget { get; set; }
        public double? AutoRestStart { get; set; }
        public int? DungeonCombatStyle { get; set; }
        public int? RaidCombatStyle { get; set; }

        /// <summary>
        ///     How many times the player has auto joined the dungeon, not to be confused with the <see cref="AutoJoinDungeonCounter"/> which is maximum amount of times to join.
        /// </summary>
        public long AutoJoinDungeonCount { get; set; }

        /// <summary>
        ///     How many times the player has auto joined the raid, not to be confused with the <see cref="AutoJoinRaidCounter"/> which is maximum amount of times to join.
        /// </summary>
        public long AutoJoinRaidCount { get; set; }

        /// <summary>
        ///     Whether or not the player is currently auto-resting
        /// </summary>
        public bool IsAutoResting { get; set; }
    }
}
