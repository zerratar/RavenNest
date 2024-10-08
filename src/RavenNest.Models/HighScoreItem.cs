﻿using System;

namespace RavenNest.Models
{
    public class HighScoreItem
    {
        public int CharacterIndex { get; set; }
        public Guid CharacterId { get; set; }
        public string PlayerName { get; set; }
        public int Rank { get; set; }
        public string Skill { get; set; }
        public double Experience { get; set; }
        public int Level { get; set; }
        public DateTime DateReached { get; set; }
        public int OrderAchieved { get; set; }
    }
}
