using System;
using System.Collections.Generic;

namespace RavenNest.DataModels
{
    public partial class Statistics
    {
        public Statistics()
        {
            Character = new HashSet<Character>();
        }

        public Guid Id { get; set; }

        public int RaidsWon { get; set; }
        public int RaidsLost { get; set; }
        public int RaidsJoined { get; set; }

        public int DuelsWon { get; set; }
        public int DuelsLost { get; set; }

        public int PlayersKilled { get; set; }
        public int EnemiesKilled { get; set; }

        public int ArenaFightsJoined { get; set; }
        public int ArenaFightsWon { get; set; }

        public long TotalDamageDone { get; set; }
        public long TotalDamageTaken { get; set; }
        public int DeathCount { get; set; }

        public decimal TotalWoodCollected { get; set; }
        public decimal TotalOreCollected { get; set; }
        public decimal TotalFishCollected { get; set; }
        public decimal TotalWheatCollected { get; set; }

        public int CraftedWeapons { get; set; }
        public int CraftedArmors { get; set; }
        public int CraftedPotions { get; set; }
        public int CraftedRings { get; set; }
        public int CraftedAmulets { get; set; }

        public int CookedFood { get; set; }

        public int ConsumedPotions { get; set; }
        public int ConsumedFood { get; set; }

        public long TotalTreesCutDown { get; set; }

        public ICollection<Character> Character { get; set; }
    }
}