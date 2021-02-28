using System;
using System.Collections.Generic;

namespace RavenNest.DataModels
{
    public partial class CharacterState : Entity<CharacterState>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private int health; public int Health { get => health; set => Set(ref health, value); }
        private string duelOpponent; public string DuelOpponent { get => duelOpponent; set => Set(ref duelOpponent, value); }
        private bool inRaid; public bool InRaid { get => inRaid; set => Set(ref inRaid, value); }
        private bool inArena; public bool InArena { get => inArena; set => Set(ref inArena, value); }
        private bool? inDungeon; public bool? InDungeon { get => inDungeon; set => Set(ref inDungeon, value); }
        private bool? inOnsen; public bool? InOnsen { get => inOnsen; set => Set(ref inOnsen, value); }
        private string task; public string Task { get => task; set => Set(ref task, value); }
        private string taskArgument; public string TaskArgument { get => taskArgument; set => Set(ref taskArgument, value); }
        private string island; public string Island { get => island; set => Set(ref island, value); }
        private decimal? x; public decimal? X { get => x; set => Set(ref x, value); }
        private decimal? y; public decimal? Y { get => y; set => Set(ref y, value); }
        private decimal? z; public decimal? Z { get => z; set => Set(ref z, value); }
        private decimal? restedTime; public decimal? RestedTime { get => restedTime; set => Set(ref restedTime, value); }
    }
}
