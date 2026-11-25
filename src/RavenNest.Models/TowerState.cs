using System;

namespace RavenNest.Models
{
    /// <summary>
    ///     Kills in the tower since the last report, per character and kind of monster: the characters' own quests
    ///     (the Mossgate chains) count them. The stream's part objectives are reported apart (api/tower/{step}/progress).
    /// </summary>
    public class TowerKillReport
    {
        public TowerCharacterKills[] Kills { get; set; }
        /// <summary>Characters knocked out during a boss attempt since the last report (a hidden achievement or two).</summary>
        public TowerCharacterFall[] Falls { get; set; }
    }

    public class TowerCharacterFall
    {
        public System.Guid CharacterId { get; set; }
        /// <summary>The step (the part) whose boss attempt it was.</summary>
        public int Floor { get; set; }
    }

    public class TowerCharacterKills
    {
        public System.Guid CharacterId { get; set; }
        /// <summary>The monster's kind (RavenNest.Combat.TowerEnemy.Id): "hall-goblin".</summary>
        public string Kind { get; set; }
        public int Count { get; set; }
    }

    /// <summary>
    ///     A stream's place in the tower as the server has it (the game's docs/tower-design.md and
    ///     docs/tower-worlds-proposal.md section 12): the part it is on, the part's objectives and how far they are,
    ///     whether the guardian is open, and how many parts the stream has cleared. Players see floors with parts;
    ///     the server counts steps (a part's number through every floor), and RavenNest.Combat.Tower maps between the
    ///     two and holds the parts themselves (names, enemies, guardians), shared by the game and the server.
    /// </summary>
    public class TowerState
    {
        /// <summary>The step the stream is on: the lowest part it has not cleared. Above TopFloor when all are cleared.</summary>
        public int Floor { get; set; }
        /// <summary>The highest step that exists so far.</summary>
        public int TopFloor { get; set; }
        /// <summary>The part's counted objectives (kills, gathering, finds) and where they stand; the guardian is not among them.</summary>
        public TowerObjectiveProgress[] Objectives { get; set; }
        /// <summary>The part's objectives are done, so its guardian can be challenged.</summary>
        public bool BossOpen { get; set; }
        /// <summary>Boss attempts made on this part.</summary>
        public int Attempts { get; set; }
        /// <summary>Parts cleared by this stream.</summary>
        public int Cleared { get; set; }
        /// <summary>The stream that cleared this part first in the world, if any has.</summary>
        public string FirstClearBy { get; set; }
    }

    public class TowerObjectiveProgress
    {
        public string Id { get; set; }
        /// <summary>RavenNest.Combat.TowerObjectiveKind: 0 kill, 1 gather, 2 find.</summary>
        public int Kind { get; set; }
        public int Count { get; set; }
        public int Amount { get; set; }
    }

    /// <summary>
    ///     What the game reports when a guardian falls: who took part, who landed the last blow, and how strong the
    ///     group was (for the record of the clear). The server decides everything that follows from it.
    /// </summary>
    public class TowerClearReport
    {
        /// <summary>Everyone in the attempt.</summary>
        public System.Guid[] Characters { get; set; }
        /// <summary>The characters whose blows landed as the guardian fell: the one that took its last health first, then any other that struck in the same moment.</summary>
        public System.Guid[] LastHits { get; set; }
        /// <summary>The group's average strength beyond a fresh level-synced player (RavenNest.Combat.TowerPressure), 1 when not known.</summary>
        public double Strength { get; set; }
    }

    /// <summary>
    ///     One of a stream's best climbers, for the stone and the statue at the foot of the tower in the main world
    ///     (the game's docs/world-tower.md): a name and a look, which the game and the website show of any character
    ///     anyway, and its part in this stream's climb.
    /// </summary>
    public class TowerClimberInfo
    {
        public System.Guid CharacterId { get; set; }
        public string Name { get; set; }
        /// <summary>How many of the stream's clears the character fought in: the measure the list is ordered by.</summary>
        public int Clears { get; set; }
        /// <summary>The highest part among them, as a step.</summary>
        public int Highest { get; set; }
        /// <summary>Floor bosses of this stream that fell to its blow.</summary>
        public int LastHits { get; set; }
        public SyntyAppearance Appearance { get; set; }
        /// <summary>
        ///     What the character wears, for dressing its statue when it is not in the game: the item each worn piece
        ///     looks like (its look where it has been given one, else itself). Pets and boats are left out. The same
        ///     the game and the website show of any character.
        /// </summary>
        public System.Guid[] Gear { get; set; }
    }

    /// <summary>The answer to a boss attempt that was won: the rewards, and the stream's state after it.</summary>
    public class TowerClearResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        /// <summary>The step that was cleared.</summary>
        public int Floor { get; set; }
        /// <summary>The guardian was the floor's boss: the next floor is open.</summary>
        public bool FloorBoss { get; set; }
        /// <summary>A floor boss, and no stream in the world had beaten it before: the world first that is announced.</summary>
        public bool WorldFirst { get; set; }
        /// <summary>No stream in the world had cleared this part before (kept for the website; coins are doubled).</summary>
        public bool PartFirst { get; set; }
        /// <summary>Coins each player who took part got.</summary>
        public long Coins { get; set; }
        public EventItemReward[] Rewards { get; set; }
        /// <summary>The characters who got the floor boss's last hit title (nobody for a mini boss): usually one, more when blows landed together.</summary>
        public System.Guid[] LastHitBy { get; set; }
        /// <summary>The pressure the server worked out for the clear (the group against the suggested one, times its strength).</summary>
        public double Pressure { get; set; }
        public TowerState State { get; set; }
    }
}
