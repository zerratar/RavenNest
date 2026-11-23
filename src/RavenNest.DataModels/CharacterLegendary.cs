using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     A character's quiet bad-luck count for the Legendary tier (the game's docs/legendary-design.md): qualifying
    ///     boss kills since its last find, which raise its chance a little each until it finds one. One per character,
    ///     made at its first qualifying kill; the table is made at startup.
    /// </summary>
    public partial class CharacterLegendary : Entity<CharacterLegendary>
    {
        [PersistentData] private Guid characterId;
        [PersistentData] private int kills;
        [PersistentData] private int finds;
        [PersistentData] private DateTime? updated;
    }
}
