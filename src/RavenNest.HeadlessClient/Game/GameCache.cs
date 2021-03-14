using RavenNest.SDK.Endpoints;

namespace RavenNest.HeadlessClient.Game
{
    public class GameCache : IGameCache
    {
        public bool IsAwaitingGameRestore => false;
    }
}
