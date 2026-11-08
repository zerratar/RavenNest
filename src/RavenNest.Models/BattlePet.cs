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
        //public string Customization { get; set; }
        public TimeSpan PlayTime { get; set; }

        // battle pets (the game's docs/battle-pets-design.md)
        /// <summary>RavenNest.Combat.PetSpecies.Id.</summary>
        public string Species { get; set; }
        public int Stage { get; set; }
        /// <summary>The pet skills in its slots, comma separated ids in slot order.</summary>
        public string Slots { get; set; }
        /// <summary>RavenNest.Combat.PetSource.</summary>
        public int Source { get; set; }
        /// <summary>A woken pet item's item id.</summary>
        public Guid? ItemId { get; set; }
        public bool Active { get; set; }
    }
}
