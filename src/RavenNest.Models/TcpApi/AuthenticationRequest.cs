using System;
using System.Collections.Generic;

namespace RavenNest.Models.TcpApi
{
    public class AuthenticationRequest
    {
        public string SessionToken { get; set; }
    }
    public class GameStateRequest
    {
        public string SessionToken { get; set; }
        public int PlayerCount { get; set; }
        public DungeonState Dungeon { get; set; }
        public RaidState Raid { get; set; }
    }

    public class RaidState
    {
        public bool IsActive { get; set; }
        public int BossCombatLevel { get; set; }
        public int CurrentBossHealth { get; set; }
        public int MaxBossHealth { get; set; }
        public int PlayersJoined { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime NextRaid { get; set; }
    }

    public class DungeonState
    {
        public bool IsActive { get; set; }
        public string Name { get; set; }
        public bool HasStarted { get; set; }
        public int BossCombatLevel { get; set; }
        public int CurrentBossHealth { get; set; }
        public int MaxBossHealth { get; set; }
        public int PlayersAlive { get; set; }
        public int PlayersJoined { get; set; }
        public int EnemiesLeft { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime NextDungeon { get; set; }
    }

    public class SaveExperienceRequest
    {
        public string SessionToken { get; set; }
        public ExperienceUpdate[] ExpUpdates { get; set; }
    }

    public class ExperienceUpdate
    {
        public Guid CharacterId { get; set; }
        public IReadOnlyList<SkillUpdate> Skills { get; set; }
    }


    public class SaveStateRequest
    {
        public string SessionToken { get; set; }
        public CharacterStateUpdate[] StateUpdates { get; set; }
    }

    public class CharacterStateUpdate
    {
        public Guid CharacterId { get; set; }
        public short Health { get; set; }
        public Island Island { get; set; }
        public Island Destination { get; set; }
        public CharacterFlags State { get; set; }
        public int TrainingSkillIndex { get; set; }
        public string TaskArgument { get; set; }
        public long ExpPerHour { get; set; }
        public DateTime EstimatedTimeForLevelUp { get; set; }
        public short X { get; set; }
        public short Y { get; set; }
        public short Z { get; set; }
        public int AutoJoinRaidCounter { get; set; }
        public int AutoJoinDungeonCounter { get; set; }

        public long AutoJoinRaidCount { get; set; }
        public long AutoJoinDungeonCount { get; set; }
        public long AutoRestCount { get; set; }

        public int AutoTrainTargetLevel { get; set; }
        public double? AutoRestTarget { get; set; }
        public double? AutoRestStart { get; set; }
        public int? DungeonCombatStyle { get; set; }
        public int? RaidCombatStyle { get; set; }
    }


    public class CharacterUpdate
    {
        // Starting with Guid seem to kill things?
        public Guid CharacterId { get; set; }
        public short Health { get; set; }
        public Island Island { get; set; }
        public Island Destination { get; set; }
        public CharacterFlags State { get; set; }
        public string Task { get; set; }
        public string TaskArgument { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public int AutoJoinRaidCounter { get; set; }
        public int AutoJoinDungeonCounter { get; set; }

        public long AutoJoinRaidCount { get; set; }
        public long AutoJoinDungeonCount { get; set; }
        public long AutoRestCount { get; set; }

        public long ExpPerHour { get; set; }
        public DateTime EstimatedTimeForLevelUp { get; set; }
        public SkillUpdate[] Skills { get; set; }
    }

    public class SkillUpdate
    {
        public byte Index { get; set; }
        public short Level { get; set; }
        public double Experience { get; set; }
    }
}
