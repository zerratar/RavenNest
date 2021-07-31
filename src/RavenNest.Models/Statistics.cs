using System;

namespace RavenNest.Models
{
    public class Statistics
    {
        public Guid Id { get; set; }

        public int RaidsWon { get; set; }
        public int RaidsLost { get; set; }
        public int RaidsJoined { get; set; }
        public int DungeonsJoined { get; set; }
        public int DungeonsWon { get; set; }
        public int DungeonsDied { get; set; }
        public int MinigameWons { get; set; }


        public int DuelsWon { get; set; }
        public int DuelsLost { get; set; }

        public int PlayersKilled { get; set; }
        public int EnemiesKilled { get; set; }

        public int ArenaFightsJoined { get; set; }
        public int ArenaFightsWon { get; set; }

        public long TotalDamageDone { get; set; }
        public long TotalDamageTaken { get; set; }
        public int DeathCount { get; set; }

        public double TotalWoodCollected { get; set; }
        public double TotalOreCollected { get; set; }
        public double TotalFishCollected { get; set; }
        public double TotalWheatCollected { get; set; }

        public int CraftedWeapons { get; set; }
        public int CraftedArmors { get; set; }
        public int CraftedPotions { get; set; }
        public int CraftedRings { get; set; }
        public int CraftedAmulets { get; set; }

        public int CookedFood { get; set; }

        public int ConsumedPotions { get; set; }
        public int ConsumedFood { get; set; }

        public long TotalTreesCutDown { get; set; }
    }
}
