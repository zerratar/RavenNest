﻿using System;

namespace RavenNest.DataModels
{
    public partial class Village : Entity<Village>
    {
        private Guid userId; public Guid UserId { get => userId; set => Set(ref userId, value); }
        private string name; public string Name { get => name; set => Set(ref name, value); }
        private int level; public int Level { get => level; set => Set(ref level, value); }
        private double experience; public double Experience { get => experience; set => Set(ref experience, value); }
        private Guid resourcesId; public Guid ResourcesId { get => resourcesId; set => Set(ref resourcesId, value); }
    }
}
