using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Github;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic
{
    public class ServerEventService : IHostedService
    {
        private readonly ILogger<ServerEventService> logger;
        private readonly IMessageBus messageBus;
        private readonly GameData gameData;
        private readonly DateTime startTime;
        private readonly List<IMessageBusSubscription> subscriptions = new List<IMessageBusSubscription>();

        private readonly object consoleTitleMutex = new object();

        public ServerEventService(
            ILogger<ServerEventService> logger,
            IMessageBus messageBus,
            GameData gameData)
        {
            this.logger = logger;
            this.messageBus = messageBus;
            this.gameData = gameData;
            this.startTime = DateTime.UtcNow;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            subscriptions.Add(this.messageBus.Subscribe("OnDeltaServerStatsUpdated", OnDeltaServerStatsUpdated));
            subscriptions.Add(this.messageBus.Subscribe("OnEventServerStatsUpdated", OnEventServerStatsUpdated));
            subscriptions.Add(this.messageBus.Subscribe("OnDataSaved", OnDataSaved));

            return Task.CompletedTask;
        }

        private void OnEventServerStatsUpdated()
        {
            UpdateConsoleTitle();
            LogTrafficStats();
        }

        private void OnDeltaServerStatsUpdated()
        {
            UpdateConsoleTitle();
            LogTrafficStats();
        }

        private void OnDataSaved()
        {
            UpdateConsoleTitle();
            LogDbStats();
        }

        private void LogDbStats()
        {
            if (gameData == null || !gameData.InitializedSuccessful)
            {
                return;
            }
            try
            {
                var lastWrite = gameData.LastDbWriteStarted;
                //var lastWrite = gameData.LastDbWriteCompleted;
                var timeSinceLastDbWrite = lastWrite > DateTime.MinValue ? DateTime.UtcNow - lastWrite : TimeSpan.Zero;
                var recordsSaved = gameData.LastDbWriteCount;
                var duration = gameData.LastDbWriteDuration;
                var sb = new StringBuilder();
                sb.AppendLine("=== DB Write ===");
                sb.AppendLine($" * Last Write: {lastWrite} ({timeSinceLastDbWrite.TotalSeconds:F1}s ago, took: {duration.TotalMilliseconds}ms), Records Saved: {recordsSaved}");
                foreach (var kvp in gameData.LastDbWriteEntities)
                {
                    sb.AppendLine($" * {kvp.Key}: {kvp.Value}");
                }
                logger.LogInformation(sb.ToString());
            }
            catch
            {
                // Ignore any exceptions when logging DB stats
            }
        }

        private void LogTrafficStats()
        {
            if (gameData == null || !gameData.InitializedSuccessful)
            {
                return;
            }
            try
            {
                var deltaStats = gameData.DeltaServerNetworkStats;
                var evtStats = gameData.EventServerNetworkStats;

                logger.LogInformation("=== Network Traffic ===\n" +
                    " * Delta Server: in={dInMessageCount} msgs ({dInTrafficKBps:F2} KB/s), out={dOutMessageCount} msgs ({dOutTrafficKBps:F2} KB/s)\n" +
                    " * Event Server: in={eInMessageCount} msgs ({eInTrafficKBps:F2} KB/s), out={eOutMessageCount} msgs ({eOutTrafficKBps:F2} KB/s)",
                    deltaStats.InMessageCount, deltaStats.InTrafficKBps, deltaStats.OutMessageCount, deltaStats.OutTrafficKBps,
                    evtStats.InMessageCount, evtStats.InTrafficKBps, evtStats.OutMessageCount, evtStats.OutTrafficKBps
                );
            }
            catch
            {
                // Ignore any exceptions when logging traffic stats
            }
        }

        private void UpdateConsoleTitle()
        {
            try
            {
                if (gameData == null || !gameData.InitializedSuccessful)
                {
                    lock (consoleTitleMutex)
                    {
                        var timeSinceLastDbWrite = gameData.LastDbWriteCompleted > DateTime.MinValue ? DateTime.UtcNow - gameData.LastDbWriteCompleted : TimeSpan.Zero;
                        var recordsSaved = gameData.LastDbWriteCount;
                        Console.Title = $"RavenNest - Starting up...";
                    }
                    return;
                }

                var evtStats = gameData.EventServerNetworkStats;
                var deltaStats = gameData.DeltaServerNetworkStats;

                lock (consoleTitleMutex)
                {
                    var timeSinceLastDbWrite = gameData.LastDbWriteCompleted > DateTime.MinValue ? DateTime.UtcNow - gameData.LastDbWriteCompleted : TimeSpan.Zero;
                    var recordsSaved = gameData.LastDbWriteCount;
                    Console.Title = $"RavenNest - {GetUptime()} - [DB: Saved {recordsSaved} records, {Math.Round(timeSinceLastDbWrite.TotalSeconds, 1)}s ago] - [Delta in: {deltaStats.InTrafficKBps:F2} KB/s] - [Event in: {evtStats.InTrafficKBps:F2} KB/s, out: {evtStats.OutTrafficKBps:F2} KB/s]";
                }
            }
            catch
            {
                // Ignore any exceptions when updating the console title
            }
        }
        private string GetUptime()
        {
            return "[Uptime: " + FormatTimeSpan(DateTime.UtcNow - startTime) + "] ";
        }

        private string FormatTimeSpan(TimeSpan elapsed)
        {
            var str = "";
            if (elapsed.Days > 0)
            {
                str += elapsed.Days + "d ";
            }
            if (elapsed.Hours > 0)
            {
                str += elapsed.Hours + "h ";
            }
            if (elapsed.Minutes > 0)
            {
                str += elapsed.Minutes + "m ";
            }

            str += elapsed.Seconds + "s";
            return str;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var subscription in subscriptions)
            {
                subscription.Unsubscribe();
            }
            return Task.CompletedTask;
        }
    }
}
