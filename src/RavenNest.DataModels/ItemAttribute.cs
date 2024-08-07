﻿using System;

namespace RavenNest.DataModels
{
    public partial class ItemAttribute : Entity<ItemAttribute>
    {
        private string name; public string Name { get => name; set => Set(ref this.name, value); }
        private string description; public string Description { get => description; set => Set(ref this.description, value); }
        private int attributeIndex; public int AttributeIndex { get => attributeIndex; set => Set(ref this.attributeIndex, value); }
        private int type; public int Type { get => type; set => Set(ref this.type, value); }
        private string maxValue; public string MaxValue { get => maxValue; set => Set(ref this.maxValue, value); }
        private string minValue; public string MinValue { get => minValue; set => Set(ref this.minValue, value); }
        private string defaultValue; public string DefaultValue { get => defaultValue; set => Set(ref this.defaultValue, value); }
    }
}
