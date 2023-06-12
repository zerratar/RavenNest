using System;
using System.Collections.Generic;

namespace RavenNest.DataModels
{
    public partial class CharacterState : Entity<CharacterState>
    {
        private int health; public int Health { get => health; set => Set(ref health, value); }
        private bool inRaid; public bool InRaid { get => inRaid; set => Set(ref inRaid, value); }
        private bool inArena; public bool InArena { get => inArena; set => Set(ref inArena, value); }
        private bool? inDungeon; public bool? InDungeon { get => inDungeon; set => Set(ref inDungeon, value); }
        private bool? inOnsen; public bool? InOnsen { get => inOnsen; set => Set(ref inOnsen, value); }
        private string task; public string Task { get => task; set => Set(ref task, value); }
        private string taskArgument; public string TaskArgument { get => taskArgument; set => Set(ref taskArgument, value); }
        private string island; public string Island { get => island; set => Set(ref island, value); }
        private string destination; public string Destination { get => destination; set => Set(ref destination, value); }
        private double? x; public double? X { get => x; set => Set(ref x, value); }
        private double? y; public double? Y { get => y; set => Set(ref y, value); }
        private double? z; public double? Z { get => z; set => Set(ref z, value); }
        private double? restedTime; public double? RestedTime { get => restedTime; set => Set(ref restedTime, value); }
    }
}
