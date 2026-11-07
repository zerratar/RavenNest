using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     A tower floor a stream cleared (the game's docs/tower-design.md): when, with how many players and after how
    ///     many attempts. The earliest clear of a floor is the world's first, named in chat and on the website.
    /// </summary>
    public partial class TowerClear : Entity<TowerClear>
    {
        [PersistentData] private Guid userId;
        [PersistentData] private int floor;
        [PersistentData] private DateTime cleared;
        [PersistentData] private int players;
        [PersistentData] private int attempts;
    }
}
