using System;

namespace RavenNest.Models
{
    /// <summary>
    ///     Kills on tower floors since the last report, per character and kind of monster: the characters' own quests
    ///     (the Mossgate chains) count them. The stream's floor quests are reported apart (api/tower/{floor}/progress).
    /// </summary>
    public class TowerKillReport
    {
        public TowerCharacterKills[] Kills { get; set; }
    }

    public class TowerCharacterKills
    {
        public System.Guid CharacterId { get; set; }
        /// <summary>The monster's kind (RavenNest.Combat.TowerEnemy.Id): "hall-goblin".</summary>
        public string Kind { get; set; }
        public int Count { get; set; }
    }

    /// <summary>
    ///     A stream's place in the tower as the server has it (the game's docs/tower-design.md): the floor it is on, its
    ///     quests and how far they are, whether the boss is open, and how many floors the stream has cleared. The floors
    ///     themselves (names, enemies, bosses) are in RavenNest.Combat.Tower, shared by the game and the server.
    /// </summary>
    public class TowerState
    {
        /// <summary>The floor the stream is on: the lowest one it has not cleared. Above TopFloor when all are cleared.</summary>
        public int Floor { get; set; }
        /// <summary>The highest floor that exists so far.</summary>
        public int TopFloor { get; set; }
        public TowerQuestProgress[] Quests { get; set; }
        /// <summary>The floor's quests are done, so the boss can be challenged.</summary>
        public bool BossOpen { get; set; }
        /// <summary>Boss attempts made on this floor.</summary>
        public int Attempts { get; set; }
        /// <summary>Floors cleared by this stream.</summary>
        public int Cleared { get; set; }
        /// <summary>The stream that cleared this floor first in the world, if any has.</summary>
        public string FirstClearBy { get; set; }
    }

    public class TowerQuestProgress
    {
        public string Id { get; set; }
        public int Count { get; set; }
        public int Amount { get; set; }
    }

    /// <summary>The answer to a boss attempt that was won: the rewards, and the stream's state after it.</summary>
    public class TowerClearResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int Floor { get; set; }
        /// <summary>No stream in the world had cleared this floor before.</summary>
        public bool WorldFirst { get; set; }
        /// <summary>Coins each player who took part got.</summary>
        public long Coins { get; set; }
        public EventItemReward[] Rewards { get; set; }
        public TowerState State { get; set; }
    }
}
