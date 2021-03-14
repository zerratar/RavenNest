using RavenNest.Models;
using RavenNest.SDK;
using System.Collections.Concurrent;

namespace RavenNest.HeadlessClient.Game
{
    public class GameManager : IGameManager
    {
        private readonly ILogger logger;
        private readonly IPlayerManager playerManager;
        private readonly ConcurrentQueue<GameEvent> gameEventQueue = new ConcurrentQueue<GameEvent>();

        public GameManager(ILogger logger, IPlayerManager playerManager)
        {
            this.logger = logger;
            this.playerManager = playerManager;
        }

        public IPlayerManager Players => playerManager;

        public void HandleGameEvents(EventList gameEvents)
        {
            logger.WriteLine("Game Events Received: " + gameEvents.Events.Count);

            foreach (var ge in gameEvents.Events)
            {
                gameEventQueue.Enqueue(ge);
            }
        }

        public int GameEventCount => gameEventQueue.Count;

        public void OnAuthenticated()
        {
        }

        public void OnSessionStart()
        {
        }
    }
}
