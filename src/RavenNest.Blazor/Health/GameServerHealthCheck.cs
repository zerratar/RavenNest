﻿using System.Collections.Generic;
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
        private readonly GameData gameData;

        public GameServerHealthCheck(IKernel kernel, GameData gameData /* GameData will start the Kernel if everything went ok*/)
        {
            this.kernel = kernel;
            this.gameData = gameData;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
        {
            var errorData = new Dictionary<string, object>();

            if (!gameData.InitializedSuccessful)
                errorData.Add(nameof(GameData), "GameData was not initialized successful");

            if (!kernel.Started)
                errorData.Add(nameof(IKernel), "Kernel not started");

            return Task.FromResult(errorData.Count > 0 ? HealthCheckResult.Unhealthy("The GameServer is not running properly: " + Newtonsoft.Json.JsonConvert.SerializeObject(errorData), data: errorData) : HealthCheckResult.Healthy());
        }
    }
}
