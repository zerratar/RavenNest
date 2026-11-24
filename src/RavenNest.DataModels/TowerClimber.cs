using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     A character's part in one stream's climb of the tower (the game's docs/world-tower.md): how many of that
    ///     stream's clears it fought in, the highest part among them (its step) and how many floor bosses fell to its
    ///     blow there. Kept from v0.9.7 on, when a guardian falls; the stream's best are carved into the stone at the
    ///     foot of the tower in the main world. (The climber achievement is the character's own record on any stream.)
    /// </summary>
    public partial class TowerClimber : Entity<TowerClimber>
    {
        [PersistentData] private Guid userId;
        [PersistentData] private Guid characterId;
        [PersistentData] private int clears;
        [PersistentData] private int highest;
        [PersistentData] private int lastHits;
        [PersistentData] private DateTime updated;
    }
}
