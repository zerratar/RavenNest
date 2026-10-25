using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     One of the streamer's story lines (phase 5 of docs/quests-design.md in the game repository): quests given
    ///     to the streamer as the village's leader, the Ironhill runestone and the banana island first. The viewers do
    ///     much of the work (gathering, bringing items, raids, dungeons, finding things); the streamer talks to the
    ///     NPC and makes the story's choices. Made from VillageStories' catalog by its key.
    /// </summary>
    public partial class VillageStory : Entity<VillageStory>
    {
        [PersistentData] private Guid streamerUserId;
        [PersistentData] private string key;
        /// <summary>The current step, from 0.</summary>
        [PersistentData] private int step;
        [PersistentData] private long progress;
        /// <summary>1 under way, 2 finished, 4 given up.</summary>
        [PersistentData] private int state;
        [PersistentData] private DateTime started;
        [PersistentData] private DateTime? completed;
        /// <summary>The choices made, by step: "6=2" (the choice at step 6 was option 2), comma separated.</summary>
        [PersistentData] private string choices;
    }
}
