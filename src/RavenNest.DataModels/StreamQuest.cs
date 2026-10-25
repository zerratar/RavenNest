using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     A stream quest: one goal the whole stream works on together, started by the streamer, with a time limit.
    ///     What it asks and gives is fixed when it starts (scaled to the players in the stream then).
    /// </summary>
    public partial class StreamQuest : Entity<StreamQuest>
    {
        /// <summary>The streamer whose stream it runs in.</summary>
        [PersistentData] private Guid streamerUserId;
        [PersistentData] private string key;

        /// <summary>gather, kill, make, levels, raid or dungeon.</summary>
        [PersistentData] private string objective;
        [PersistentData] private int skill;
        [PersistentData] private long amount;
        [PersistentData] private long progress;

        /// <summary>1 running, 2 done, 3 out of time, 4 stopped.</summary>
        [PersistentData] private int state;

        /// <summary>The players in the stream when it started, which the target and rewards were scaled to.</summary>
        [PersistentData] private int players;
        [PersistentData] private DateTime started;
        [PersistentData] private DateTime ends;
        [PersistentData] private DateTime? completed;

        [PersistentData] private long rewardVillageCoins;

        /// <summary>The resource the village gets as well (1 wood, 2 ore, 3 fish, 4 wheat, 0 none) and how much.</summary>
        [PersistentData] private int rewardResource;
        [PersistentData] private long rewardResourceAmount;

        /// <summary>The coins shared among the helpers by how much each added.</summary>
        [PersistentData] private long rewardHelperCoins;
    }
}
