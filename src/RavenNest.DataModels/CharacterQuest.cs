using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     One of a character's quests: offered for the day or the week, taken, finished, expired or given up. Made
    ///     from a template of QuestManager's catalog; what it asks and gives is fixed when it is offered. A chain is
    ///     one row that moves through its steps: the objective fields hold the current step's.
    /// </summary>
    public partial class CharacterQuest : Entity<CharacterQuest>
    {
        [PersistentData] private Guid characterId;

        /// <summary>The catalog template it was made from.</summary>
        [PersistentData] private string key;

        /// <summary>0 daily, 1 weekly, 2 seasonal daily, 3 chain.</summary>
        [PersistentData] private int kind;

        /// <summary>The day or week it belongs to: "2026-10-25" or "2026-W43".</summary>
        [PersistentData] private string period;

        /// <summary>Which of the day's offers it is (0 to 2), so !quest accept 2 always means the same one.</summary>
        [PersistentData] private int slot;

        /// <summary>gather, exp, craft, cook, brew, kill, raid, dungeon, or for chain steps deliver, visit or answer.</summary>
        [PersistentData] private string objective;

        /// <summary>The skill it is about (its index), or -1.</summary>
        [PersistentData] private int skill;

        [PersistentData] private long amount;
        [PersistentData] private long progress;

        /// <summary>0 offered, 1 taken, 2 finished, 3 expired, 4 given up.</summary>
        [PersistentData] private int state;

        [PersistentData] private long rewardCoins;
        [PersistentData] private double rewardExp;

        /// <summary>The skill the experience goes to (its index).</summary>
        [PersistentData] private int rewardSkill;

        [PersistentData] private DateTime offered;
        [PersistentData] private DateTime? accepted;
        [PersistentData] private DateTime? completed;
        [PersistentData] private DateTime expires;
        /// <summary>A chain's current step, from 0.</summary>
        [PersistentData] private int step;
        /// <summary>What a chain step names: the item to deliver, or the island to visit.</summary>
        [PersistentData] private string target;
    }
}
