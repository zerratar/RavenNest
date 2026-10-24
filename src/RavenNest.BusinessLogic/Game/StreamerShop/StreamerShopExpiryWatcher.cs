using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RavenNest.BusinessLogic.Game.StreamerShop
{
    /// <summary>
    ///     Returns tavern shelf listings to their streamers once they have run out of time.
    /// </summary>
    /// <remarks>
    ///     The same rhythm as <see cref="MarketplaceExpiryWatcher"/>: a listing standing a few minutes
    ///     past its time harms nobody (buying checks the time anyway), and the start delay keeps the
    ///     sweep out of the way while the data layer is still loading.
    /// </remarks>
    public class StreamerShopExpiryWatcher : IHostedService, IDisposable
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(2);

        private readonly ILogger<StreamerShopExpiryWatcher> logger;
        private readonly StreamerShopManager shop;

        private CancellationTokenSource cts;
        private Task worker;

        public StreamerShopExpiryWatcher(ILogger<StreamerShopExpiryWatcher> logger, StreamerShopManager shop)
        {
            this.logger = logger;
            this.shop = shop;
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
                logger.LogError("Tavern shelf expiry watcher did not stop cleanly: " + exc);
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
                    var returned = shop.ReturnExpiredListings();
                    if (returned > 0) logger.LogInformation("Returned " + returned + " expired tavern shelf listings.");
                }
                catch (Exception exc)
                {
                    // one bad sweep must not end the watcher; the next one tries again
                    logger.LogError("Returning expired tavern shelf listings failed: " + exc);
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

        public void Dispose()
        {
            cts?.Dispose();
        }
    }
}
