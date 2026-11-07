using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     A stream's climb of the tower (the game's docs/tower-design.md): the floor it is on (the lowest one not yet
    ///     cleared), the quest progress on that floor in RavenNest.Combat.Tower's text form ("defeat-hall-goblin:12;..."),
    ///     and the boss attempts made on it. One per streamer, made the first time the stream reaches the tower; the
    ///     table is made at startup.
    /// </summary>
    public partial class StreamTower : Entity<StreamTower>
    {
        [PersistentData] private Guid userId;
        [PersistentData] private int floor;
        [PersistentData] private string progress;
        [PersistentData] private int attempts;
        [PersistentData] private DateTime created;
        [PersistentData] private DateTime? updated;
    }
}
