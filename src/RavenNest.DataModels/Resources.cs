using System;
using System.Collections.Generic;

namespace RavenNest.DataModels
{
    public partial class Resources : Entity<Resources>
    {
        private double wood; public double Wood { get => wood; set => Set(ref wood, value); }
        private double ore; public double Ore { get => ore; set => Set(ref ore, value); }
        private double fish; public double Fish { get => fish; set => Set(ref fish, value); }
        private double wheat; public double Wheat { get => wheat; set => Set(ref wheat, value); }
        private double magic; public double Magic { get => magic; set => Set(ref magic, value); }
        private double arrows; public double Arrows { get => arrows; set => Set(ref arrows, value); }
        private double coins; public double Coins { get => coins; set => Set(ref coins, value); }
    }
}
