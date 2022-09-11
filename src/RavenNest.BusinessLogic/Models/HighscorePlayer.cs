using RavenNest.DataModels;
using System;

namespace RavenNest.BusinessLogic.Models
{
    public class HighscorePlayer
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int CharacterIndex { get; set; }
        public Skills Skills { get; set; }
    }
}
