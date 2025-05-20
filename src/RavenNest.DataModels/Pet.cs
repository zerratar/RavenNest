using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class Pet : Entity<Pet>
    {
        [PersistentData] private Guid characterId;
        [PersistentData] private PetType type;
        [PersistentData] private PetTier tier;
        [PersistentData] private string name;
        [PersistentData] private DateTime dateOfBirth;
        [PersistentData] private int level;
        [PersistentData] private long experience;
        [PersistentData] private int attack;
        [PersistentData] private int defense;
        [PersistentData] private int strength;
        [PersistentData] private int health;
        [PersistentData] private int currentHealth;
        [PersistentData] private float happiness;
        [PersistentData] private float hunger;
        [PersistentData] private string prefab;
        [PersistentData] private TimeSpan playTime;
        [PersistentData] private bool active;
    }
}
