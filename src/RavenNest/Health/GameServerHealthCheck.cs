using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;

namespace RavenNest.Health
{
    public class GameServerHealthCheck : IHealthCheck
    {
        private readonly IKernel kernel;
        private readonly IGameData gameData;

        public GameServerHealthCheck(IKernel kernel, IGameData gameData)
        {
            this.kernel = kernel;
            this.gameData = gameData;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
        {
            if (kernel.Started)
                return Task.FromResult(HealthCheckResult.Healthy());

            return Task.FromResult(HealthCheckResult.Unhealthy("Kernel not started"));
        }
    }
}
