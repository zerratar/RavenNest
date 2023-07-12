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
        public long ExpPerHour { get; set; }
        public DateTime EstimatedTimeForLevelUp { get; set; }
        public short X { get; set; }
        public short Y { get; set; }
        public short Z { get; set; }
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

    public enum Island : byte
    {
        Ferry = 0,
        Home = 1,
        Away = 2,
        Ironhill = 3,
        Kyo = 4,
        Heim = 5
    }

    [Flags]
    public enum CharacterFlags : int
    {
        None = 0,
        InRaid = /*         */ 0b000000001,
        InArena = /*        */ 0b000000010,
        InDungeon = /*      */ 0b000000100,
        InOnsen = /*        */ 0b000001000,
        InDuel = /*         */ 0b000010000,
        InStreamRaidWar = /**/ 0b000100000,
        InDungeonQueue = /* */ 0b001000000,
        OnFerry = /*        */ 0b010000000,
        IsCaptain = /*      */ 0b100000000,
    }
}
