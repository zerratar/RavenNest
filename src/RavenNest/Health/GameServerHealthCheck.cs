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

        public GameServerHealthCheck(IKernel kernel)
        {
            this.kernel = kernel;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
        {
            if (kernel.Started)
                return Task.FromResult(HealthCheckResult.Healthy());

            return Task.FromResult(HealthCheckResult.Unhealthy("Kernel not started"));
        }
    }
}
