using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    ///     Returns marketplace listings to their sellers once they have run out of time.
    /// </summary>
    /// <remarks>
    ///     Listings have carried an expiry date since they were introduced and nothing ever acted
    ///     on it. Expiring them was an admin opening the marketplace page and pressing a button, so
    ///     a listing sat expired for as long as nobody happened to look, and the seller's items sat
    ///     with it. The page showed "Expired" against rows that were still very much for sale.
    ///
    ///     <para>
    ///     Deliberately slow. A listing standing an extra few minutes past three weeks harms
    ///     nobody, and the sweep walks every listing, so there is no reason to do it often. The
    ///     start delay keeps it out of the way while the data layer is still loading.
    ///     </para>
    /// </remarks>
    public class MarketplaceExpiryWatcher : IHostedService, IDisposable
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(2);

        private readonly ILogger<MarketplaceExpiryWatcher> logger;
        private readonly MarketplaceManager marketplace;

        private CancellationTokenSource cts;
        private Task worker;

        public MarketplaceExpiryWatcher(
            ILogger<MarketplaceExpiryWatcher> logger,
            MarketplaceManager marketplace)
        {
            this.logger = logger;
            this.marketplace = marketplace;
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
                logger.LogError("Marketplace expiry watcher did not stop cleanly: " + exc);
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
                        var returned = marketplace.ReturnExpiredListings();
                        if (returned > 0)
                        {
                            logger.LogInformation($"Marketplace: returned {returned} expired listing(s) to their sellers.");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception exc)
                    {
                        // One bad sweep must not kill the loop. Anything missed is picked up on the
                        // next pass, since expiry is decided from the stored date rather than from
                        // having been seen.
                        logger.LogError("Marketplace expiry sweep failed: " + exc);
                    }

                    await Task.Delay(CheckInterval, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }

        public void Dispose()
        {
            cts?.Dispose();
        }
    }
}
