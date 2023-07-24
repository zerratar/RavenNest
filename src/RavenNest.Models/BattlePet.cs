using System;

namespace RavenNest.Models
{
    public class BattlePet
    {
        public BattlePet() { }
        public Guid Id { get; set; }
        public Guid CharacterId { get; set; }
        public BattlePetType Type { get; set; }
        public BattlePetTier Tier { get; set; }
        public string Name { get; set; }
        public DateTime DateOfBirth { get; set; }
        public int Level { get; set; }
        public long Experience { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int Strength { get; set; }
        public int Health { get; set; }
        public int CurrentHealth { get; set; }
        public float Happiness { get; set; }
        public float Hunger { get; set; }
        public string Prefab { get; set; }
        public TimeSpan PlayTime { get; set; }
    }
}
