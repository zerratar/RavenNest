using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    ///     Picks up a new GitHub release by itself: the newest game version and its update link.
    /// </summary>
    /// <remarks>
    ///     The server only learned about a release when a game asked for updates (api/version/check) or
    ///     someone used a download link, so after publishing the website kept calling the previous version
    ///     the newest until the first streamer started their game. GitHub answers are cached for 5 minutes
    ///     (Github.cs), so asking that often costs one request. The version never goes down, as on the
    ///     download links.
    /// </remarks>
    public class GameReleaseWatcher : IHostedService, IDisposable
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(1);

        private readonly ILogger<GameReleaseWatcher> logger;
        private readonly GameData gameData;

        private CancellationTokenSource cts;
        private Task worker;

        public GameReleaseWatcher(ILogger<GameReleaseWatcher> logger, GameData gameData)
        {
            this.logger = logger;
            this.gameData = gameData;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            worker = Task.Run(() => RunAsync(cts.Token));
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                cts?.Cancel();
                if (worker != null) await worker;
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
            catch (Exception exc)
            {
                logger.LogError("Game release watcher did not stop cleanly: " + exc);
            }
        }

        private async Task RunAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(StartDelay, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await CheckAsync();
                }
                catch (Exception exc)
                {
                    // GitHub being away must not end the watcher; the next check tries again
                    logger.LogError("Checking GitHub for a new game release failed: " + exc.Message);
                }

                try
                {
                    await Task.Delay(CheckInterval, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private async Task CheckAsync()
        {
            var client = gameData.Client;
            if (client == null) return;

            var release = await RavenNest.BusinessLogic.Github.Github.GetGithubReleaseAsync();
            if (release == null || release.Version < gameData.GetClientVersion() || client.ClientVersion == release.VersionString)
                return;

            logger.LogInformation("New game release on GitHub: " + release.VersionString + " (was " + client.ClientVersion + ").");
            client.ClientVersion = release.VersionString;
            client.DownloadLink = release.UpdateDownloadUrl_Win;
        }

        public void Dispose()
        {
            cts?.Dispose();
        }
    }
}
