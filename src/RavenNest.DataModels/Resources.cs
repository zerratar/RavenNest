using RavenNest.DataAnnotations;
using System;
using System.Collections.Generic;

namespace RavenNest.DataModels
{
    public partial class Resources : Entity<Resources>
    {
        [PersistentData] private double wood;
        [PersistentData] private double ore;
        [PersistentData] private double fish;
        [PersistentData] private double wheat;
        [PersistentData] private double magic;
        [PersistentData] private double arrows;
        [PersistentData] private double coins;
    }
}
