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

        // battle pets (the game's docs/battle-pets-design.md); the columns are added at startup
        /// <summary>RavenNest.Combat.PetSpecies.Id, such as "bunny" or "companion-raven".</summary>
        [PersistentData] private string species;
        /// <summary>Evolution stage, 0 to 2.</summary>
        [PersistentData] private int stage;
        /// <summary>The pet skills in its slots, comma separated ids in slot order.</summary>
        [PersistentData] private string slots;
        /// <summary>RavenNest.Combat.PetSource: tamed, a woken pet item, an egg.</summary>
        [PersistentData] private int source;
        /// <summary>A woken pet item's item id: the pet comes out only while its owner has one.</summary>
        [PersistentData] private Guid? itemId;
    }
}
