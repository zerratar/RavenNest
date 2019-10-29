using System;

namespace RavenNest.DataModels
{
    public partial class GameEvent
    {
        public Guid Id { get; set; }
        public Guid GameSessionId { get; set; }
        public int Type { get; set; }
        public int Revision { get; set; }
        public string Data { get; set; }

        //public GameSession GameSession { get; set; }
    }
}