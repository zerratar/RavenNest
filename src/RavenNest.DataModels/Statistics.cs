using RavenNest.DataAnnotations;
using System;
using System.Collections.Generic;

namespace RavenNest.DataModels
{
    public partial class Statistics : Entity<Statistics>
    {
        [PersistentData] private int raidsWon;
        [PersistentData] private int raidsLost;
        [PersistentData] private int raidsJoined;
        [PersistentData] private int duelsWon;
        [PersistentData] private int duelsLost;
        [PersistentData] private int playersKilled;
        [PersistentData] private int enemiesKilled;
        [PersistentData] private int arenaFightsJoined;
        [PersistentData] private int arenaFightsWon;
        [PersistentData] private long totalDamageDone;
        [PersistentData] private long totalDamageTaken;
        [PersistentData] private int deathCount;
        [PersistentData] private double totalWoodCollected;
        [PersistentData] private double totalOreCollected;
        [PersistentData] private double totalFishCollected;
        [PersistentData] private double totalWheatCollected;
        [PersistentData] private int craftedWeapons;
        [PersistentData] private int craftedArmors;
        [PersistentData] private int craftedPotions;
        [PersistentData] private int craftedRings;
        [PersistentData] private int craftedAmulets;
        [PersistentData] private int cookedFood;
        [PersistentData] private int consumedPotions;
        [PersistentData] private int consumedFood;
        [PersistentData] private long totalTreesCutDown;
    }
}
