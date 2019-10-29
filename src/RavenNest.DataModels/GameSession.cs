using System;
using System.Collections.Generic;

namespace RavenNest.DataModels
{
    public partial class GameSession
    {
        public GameSession()
        {
            //CharacterSession = new HashSet<CharacterSession>();
        }

        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public DateTime Started { get; set; }
        public DateTime? Stopped { get; set; }
        public int Status { get; set; }

        [Obsolete("Will be removed in the future, do not use. As we will not support local players")]
        public bool Local { get; set; }
        public User User { get; set; }
        public long? Revision { get; set; }
        //public ICollection<CharacterSession> CharacterSession { get; set; }
        //public ICollection<GameEvent> GameEvents { get; set; }
    }
}
