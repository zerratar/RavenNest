using System;
using System.Threading.Tasks;

namespace RavenNest.HeadlessClient
{
    public interface IGameClient : IDisposable
    {
        Task<bool> AuthenticateAsync();
        Task<bool> BeginGameSessionAsync();
        Task<bool> WaitForGameEventsAsync(int eventCount, TimeSpan timeout);
        Task DownloadBackupAsync();
    }
}
