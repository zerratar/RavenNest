using RavenNest.Models;
using System;
using System.Threading.Tasks;

namespace RavenNest.HeadlessClient
{
    public interface IGameClient : IDisposable
    {
        SessionToken SessionToken { get; }
        Task<bool> AuthenticateAsync();
        Task<bool> BeginGameSessionAsync();
        Task<bool> WaitForGameEventsAsync(int eventCount, TimeSpan timeout);
        Task DownloadBackupAsync();
    }
}
