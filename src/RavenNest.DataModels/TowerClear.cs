using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     A part of the tower a stream cleared (the game's docs/tower-design.md; "floor" is the part's step, from when
    ///     parts were called floors): when, with how many players, how strong they were and after how many attempts.
    ///     The earliest clear of a part is the world's first; a floor boss's (the last part of a floor) is named in
    ///     chat and on the website, the others are kept for the website. A floor boss's clear also names who landed
    ///     the last blow.
    /// </summary>
    public partial class TowerClear : Entity<TowerClear>
    {
        [PersistentData] private Guid userId;
        [PersistentData] private int floor;
        [PersistentData] private DateTime cleared;
        [PersistentData] private int players;
        [PersistentData] private int attempts;
        [PersistentData] private double? strength;
        [PersistentData] private Guid? lastHitCharacterId;
    }
}
