using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public class Village
    {
        public Guid Id { get; set; }
        public string Owner { get; set; }
        public string Name { get; set; }
        public Resources Resources { get; set; }
        public IReadOnlyList<VillageHouse> Houses { get; set; }
    }
}