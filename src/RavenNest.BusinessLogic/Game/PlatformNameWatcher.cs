using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.Twitch;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// Watches live streamers for platform renames and applies them while they are still streaming.
    /// </summary>
    /// <remarks>
    /// Session start already resolves the current name, which covers someone who renames themselves
    /// and then launches the game. This covers the other order: renaming part way through a stream,
    /// where nothing would otherwise notice until the next session start or bot reconnect.
    ///
    /// <para>
    /// Lookups are keyed on the Twitch account id, which a rename does not change, and are batched
    /// so the whole set of live streamers costs a couple of requests per sweep rather than one each.
    /// Only accounts whose name actually changed are written back and pushed to the bot.
    /// </para>
    /// </remarks>
    public class PlatformNameWatcher : IHostedService, IDisposable
    {
        /// <summary>
        /// How often live streamers are checked. A rename is rare and not urgent to the minute, so
        /// this is deliberately slow: it keeps the request count low and avoids writing to accounts
        /// more often than there is any reason to.
        /// </summary>
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Delay before the first sweep, so startup is not competing with data being loaded.
        /// </summary>
        private static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(1);

        private readonly ILogger<PlatformNameWatcher> logger;
        private readonly GameData gameData;
        private readonly ITwitchClient twitchClient;
        private readonly IRavenBotApiClient ravenbotApi;

        private CancellationTokenSource cts;
        private Task worker;

        public PlatformNameWatcher(
            ILogger<PlatformNameWatcher> logger,
            GameData gameData,
            ITwitchClient twitchClient,
            IRavenBotApiClient ravenbotApi)
        {
            this.logger = logger;
            this.gameData = gameData;
            this.twitchClient = twitchClient;
            this.ravenbotApi = ravenbotApi;
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

                if (worker != null)
                {
                    await worker;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
            catch (Exception exc)
            {
                logger.LogError("Platform name watcher did not stop cleanly: " + exc);
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(StartDelay, cancellationToken);

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await CheckActiveSessionsAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception exc)
                    {
                        // One bad sweep must not kill the loop. Renames are picked up on the next
                        // pass, and session start still covers the common case regardless.
                        logger.LogError("Platform name check failed: " + exc);
                    }

                    await Task.Delay(CheckInterval, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }

        private async Task CheckActiveSessionsAsync(CancellationToken cancellationToken)
        {
            // Streamer account id keyed by Twitch id, so the response can be matched back without
            // searching. Twitch returns the accounts in no guaranteed order.
            var byPlatformId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

            foreach (var session in gameData.GetActiveSessions())
            {
                if (session == null)
                {
                    continue;
                }

                var access = gameData.GetUserAccess(session.UserId, "twitch");
                if (access == null || string.IsNullOrEmpty(access.PlatformId))
                {
                    continue;
                }

                byPlatformId[access.PlatformId] = session.UserId;
            }

            if (byPlatformId.Count == 0)
            {
                return;
            }

            var platformIds = byPlatformId.Keys.ToList();

            for (var i = 0; i < platformIds.Count; i += TwitchRequests.MaxUserLookupBatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batch = platformIds
                    .Skip(i)
                    .Take(TwitchRequests.MaxUserLookupBatchSize)
                    .ToList();

                var twitchUsers = await twitchClient.GetUsersByIdAsync(batch);

                foreach (var twitchUser in twitchUsers)
                {
                    if (twitchUser == null || string.IsNullOrEmpty(twitchUser.Id) || string.IsNullOrEmpty(twitchUser.Login))
                    {
                        continue;
                    }

                    if (!byPlatformId.TryGetValue(twitchUser.Id, out var userId))
                    {
                        continue;
                    }

                    await ApplyIfChangedAsync(userId, twitchUser);
                }
            }
        }

        private async Task ApplyIfChangedAsync(Guid userId, TwitchRequests.TwitchUser twitchUser)
        {
            var user = gameData.GetUser(userId);
            if (user == null)
            {
                return;
            }

            if (!UserNameSync.Apply(gameData, user, "twitch", twitchUser.Id, twitchUser.Login, twitchUser.DisplayName))
            {
                return;
            }

            logger.LogWarning("Twitch name change picked up mid stream for " + userId
                + ". Now known as '" + twitchUser.Login + "'. All stored names updated.");

            // Written locally first so the file on disk is current, then pushed over the network,
            // which is the part that actually reaches a bot on another machine.
            await ravenbotApi.UpdateUserSettingsAsync(userId);
            await ravenbotApi.PushUserNamesAsync(userId);
        }

        public void Dispose()
        {
            cts?.Dispose();
        }
    }
}
