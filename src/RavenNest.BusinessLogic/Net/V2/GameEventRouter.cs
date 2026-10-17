using System;

namespace RavenNest.BusinessLogic.Net.V2
{
    /// <summary>
    /// Lets GameData hand a game event to the streamer's v2 session without depending on the connection hub (which
    /// depends on GameData). The hub sets <see cref="Handler"/> when it starts; until then, and for streamers without
    /// a v2 session, events take the old path.
    /// </summary>
    public sealed class GameEventRouter
    {
        public Func<DataModels.GameEvent, bool> Handler { get; set; }

        /// <summary>True when a v2 session took the event.</summary>
        public bool TryRoute(DataModels.GameEvent gameEvent)
        {
            var handler = Handler;
            return handler != null && handler(gameEvent);
        }
    }
}
