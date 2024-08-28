using System;

namespace RavenNest.Models
{
    public class PlayerRestoreData
    {
        /// <summary>
        ///     Explicit Characters to add back to the game identified by character ID's.
        ///     If this value is left empty or null, 
        ///     all players that the server knows of to have been part of the game will be added.
        /// </summary>
        public Guid[] Characters { get; set; }

        public bool ForceAdd { get; set; }
    }
}
