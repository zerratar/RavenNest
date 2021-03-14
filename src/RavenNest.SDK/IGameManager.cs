using RavenNest.Models;

namespace RavenNest.SDK
{
    public interface IGameManager
    {
        public IPlayerManager Players { get; }
        int GameEventCount { get; }
        void HandleGameEvents(EventList gameEvents);
        void OnAuthenticated();
        void OnSessionStart();
    }

    public interface IPlayerManager
    {
        IPlayerController GetPlayerByUserId(string source);
    }
}
