using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Data.Aggregators
{
    public abstract class DataAggregator : IDisposable
    {
        protected readonly GameData gameData;
        private readonly Timer timer;
        protected readonly TimeSpan aggregationTime;
        protected readonly TimeSpan retentionTime;
        private readonly TimeSpan aggregationFrequency;
        private bool disposed;

        protected DataAggregator(GameData gameData, TimeSpan aggregationTime, TimeSpan retentionTime, TimeSpan aggregationFrequency)
        {
            this.gameData = gameData;
            this.aggregationTime = aggregationTime;
            this.retentionTime = retentionTime;
            this.aggregationFrequency = aggregationFrequency;

            var nextAggregation = DateTime.UtcNow.Date.Add(aggregationTime);
            if (nextAggregation <= DateTime.UtcNow)
            {
                nextAggregation = nextAggregation.Add(aggregationFrequency);
            }

            var interval = nextAggregation - DateTime.UtcNow;
            timer = new Timer(OnTimerTick, null, interval, aggregationFrequency);
        }

        private void OnTimerTick(object state)
        {
            Task.Run(() => AggregateReport());
        }

        protected abstract void AggregateReport();

        protected abstract void RemoveOldReports();

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    timer.Dispose();
                }

                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
