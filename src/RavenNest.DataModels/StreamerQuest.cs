using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     A quest a streamer wrote for their stream (phase 4 of docs/quests-design.md in the game repository): a
    ///     bounty (each viewer who reaches the target gets the reward, the first Winners of them) or a stream goal (the
    ///     whole stream works on one target and shares the reward). The reward is set aside when it is posted: coins
    ///     from the village treasury or the streamer's character, items from the streamer's stash or character, and
    ///     loyalty points from the streamer's weekly budget. What is left goes back when it ends.
    /// </summary>
    public partial class StreamerQuest : Entity<StreamerQuest>
    {
        [PersistentData] private Guid streamerUserId;
        /// <summary>1 bounty, 2 stream goal.</summary>
        [PersistentData] private int kind;
        [PersistentData] private string name;
        [PersistentData] private string description;
        /// <summary>gather, kill, craft, cook, brew, levels, raid or dungeon.</summary>
        [PersistentData] private string objective;
        [PersistentData] private int skill;
        /// <summary>The target: per viewer for a bounty, for the whole stream for a goal.</summary>
        [PersistentData] private long amount;
        /// <summary>A goal's progress.</summary>
        [PersistentData] private long progress;
        /// <summary>A bounty's number of rewards; a goal's top helpers who get the item.</summary>
        [PersistentData] private int winners;
        [PersistentData] private int claimed;
        /// <summary>1 running, 2 done, 3 out of time, 4 stopped, 5 every reward claimed.</summary>
        [PersistentData] private int state;
        [PersistentData] private DateTime started;
        [PersistentData] private DateTime ends;
        [PersistentData] private DateTime? completed;

        /// <summary>Coins per winner (a bounty) or for the helpers to share (a goal).</summary>
        [PersistentData] private long rewardCoins;
        /// <summary>0 none, 1 the village treasury, 2 the streamer's character.</summary>
        [PersistentData] private int coinSource;
        /// <summary>The village, or the streamer's character, the coins came from.</summary>
        [PersistentData] private Guid? coinSourceId;

        [PersistentData] private Guid? rewardItemId;
        [PersistentData] private string rewardItemTag;
        [PersistentData] private string rewardItemEnchantment;
        [PersistentData] private Guid? rewardItemTransmogrificationId;
        [PersistentData] private string rewardItemName;
        [PersistentData] private int rewardItemFlags;
        /// <summary>Items per winner.</summary>
        [PersistentData] private long rewardItemAmount;
        /// <summary>0 the stash, 1 a character (as StreamerShopSourceKind).</summary>
        [PersistentData] private int itemSourceKind;
        [PersistentData] private Guid? itemSourceCharacterId;

        /// <summary>Loyalty points per winner (a bounty) or for the helpers to share (a goal).</summary>
        [PersistentData] private long rewardLoyalty;
        /// <summary>The budget week the points came from, "2026-W43".</summary>
        [PersistentData] private string loyaltyWeek;

        /// <summary>What is still set aside.</summary>
        [PersistentData] private long escrowCoins;
        [PersistentData] private long escrowItems;
        [PersistentData] private long escrowLoyalty;
    }
}
