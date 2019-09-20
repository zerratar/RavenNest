using System;
using System.Collections.Generic;

namespace RavenNest.DataModels
{
    public partial class Skills
    {
        public Skills()
        {
            Character = new HashSet<Character>();
        }

        public Guid Id { get; set; }
        public decimal Attack { get; set; }
        public decimal Defense { get; set; }
        public decimal Strength { get; set; }
        public decimal Health { get; set; }
        public decimal Magic { get; set; }
        public decimal Ranged { get; set; }
        public decimal Woodcutting { get; set; }
        public decimal Fishing { get; set; }
        public decimal Mining { get; set; }
        public decimal Crafting { get; set; }
        public decimal Cooking { get; set; }
        public decimal Farming { get; set; }
        public decimal Slayer { get; set; }
        public decimal Sailing { get; set; }
        public ICollection<Character> Character { get; set; }
    }
}

