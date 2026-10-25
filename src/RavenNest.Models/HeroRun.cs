using System;

namespace RavenNest.Models
{
    /// <summary>A streamer's wave run in hero mode (the game's WaveRun): how far it got.</summary>
    public class HeroRunRecord
    {
        public string Streamer { get; set; }
        public int Waves { get; set; }
        public int Kills { get; set; }
        public int Seconds { get; set; }
        public DateTime Reached { get; set; }
    }

    /// <summary>What recording a run did: the streamer's best (new or not), titles it unlocked, the best's place.</summary>
    public class HeroRunResult
    {
        public bool Ok { get; set; }
        /// <summary>notinsession.</summary>
        public string Error { get; set; }
        public HeroRunRecord Best { get; set; }
        public bool NewBest { get; set; }
        public string[] NewTitles { get; set; }
        public int Rank { get; set; }
    }

    /// <summary>The streamers' best runs, the best first.</summary>
    public class HeroLeaderboard
    {
        public HeroRunRecord[] Entries { get; set; }
    }
}
