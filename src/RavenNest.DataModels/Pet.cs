using System;

namespace RavenNest.DataModels
{
    public partial class Pet : Entity<Pet>
    {
        private Guid characterId; public Guid CharacterId { get => characterId; set => Set(ref characterId, value); }
        private PetType type; public PetType Type { get => type; set => Set(ref type, value); }
        private PetTier tier; public PetTier Tier { get => tier; set => Set(ref tier, value); }
        private string name; public string Name { get => name; set => Set(ref name, value); }
        private DateTime dateOfBirth; public DateTime DateOfBirth { get => dateOfBirth; set => Set(ref dateOfBirth, value); }
        private int level; public int Level { get => level; set => Set(ref level, value); }
        private long experience; public long Experience { get => experience; set => Set(ref experience, value); }
        private int attack; public int Attack { get => attack; set => Set(ref attack, value); }
        private int defense; public int Defense { get => defense; set => Set(ref defense, value); }
        private int strength; public int Strength { get => strength; set => Set(ref strength, value); }
        private int health; public int Health { get => health; set => Set(ref health, value); }
        private int currentHealth; public int CurrentHealth { get => currentHealth; set => Set(ref currentHealth, value); }
        private float happiness; public float Happiness { get => happiness; set => Set(ref happiness, value); }
        private float hunger; public float Hunger { get => hunger; set => Set(ref hunger, value); }
        private string prefab; public string Prefab { get => prefab; set => Set(ref prefab, value); }
        //private string customization; public string Customization { get => customization; set => Set(ref customization, value); }
        private TimeSpan playTime; public TimeSpan PlayTime { get => playTime; set => Set(ref playTime, value); }
        private bool active; public bool Active { get => active; set => Set(ref active, value); }
    }
}
