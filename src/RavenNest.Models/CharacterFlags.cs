using System;

namespace RavenNest.Models
{
    [Flags]
    public enum CharacterFlags : int
    {
        None = 0,
        InRaid = /*         */ 0b000000001,
        InArena = /*        */ 0b000000010,
        InDungeon = /*      */ 0b000000100,
        InOnsen = /*        */ 0b000001000,
        InDuel = /*         */ 0b000010000,
        InStreamRaidWar = /**/ 0b000100000,
        InDungeonQueue = /* */ 0b001000000,
        OnFerry = /*        */ 0b010000000,
        IsCaptain = /*      */ 0b100000000,
    }
}
