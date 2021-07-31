using System;

namespace RavenNest
{
    [Serializable]
    public class Statistics
    {

        public int RaidsWon;
        public int RaidsLost;
        public int RaidsJoined;

        public int DuelsWon;
        public int DuelsLost;

        public int PlayersKilled;
        public int EnemiesKilled;

        public int ArenaFightsJoined;
        public int ArenaFightsWon;

        public long TotalDamageDone;
        public long TotalDamageTaken;
        public int DeathCount;

        public double TotalWoodCollected;
        public double TotalOreCollected;
        public double TotalFishCollected;
        public double TotalWheatCollected;

        public int CraftedWeapons;
        public int CraftedArmors;
        public int CraftedPotions;
        public int CraftedRings;
        public int CraftedAmulets;

        public int CookedFood;

        public int ConsumedPotions;
        public int ConsumedFood;

        public long TotalTreesCutDown;
    }
}
