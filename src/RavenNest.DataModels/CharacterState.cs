using System;
using System.Collections.Generic;

namespace RavenNest.DataModels
{
    public partial class CharacterState
    {
        public CharacterState()
        {
            //Character = new HashSet<Character>();
        }

        public Guid Id { get; set; }
        public int Health { get; set; }
        public string DuelOpponent { get; set; }
        public bool InRaid { get; set; }
        public bool InArena { get; set; }
        public string Task { get; set; }
        public string TaskArgument { get; set; }
        public string Island { get; set; }
        [Obsolete("Will be removed in the future")]
        public decimal? X { get; set; }
        [Obsolete("Will be removed in the future")]
        public decimal? Y { get; set; }
        [Obsolete("Will be removed in the future")]
        public decimal? Z { get; set; }
        //public ICollection<Character> Character { get; set; }
    }
}