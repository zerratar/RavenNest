using System.Collections.Generic;

namespace RavenNest.Models
{
    public class HighScoreCollection
    {
        public IReadOnlyList<HighScoreItem> Players { get; set; }
        public string Skill { get; set; }
        public int Offset { get; set; }
        public int Total { get; set; }
    }
}