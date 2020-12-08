using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Sessions;
using System;

namespace RavenNest.Blazor.Services
{
    public class ServerService : RavenNestService
    {
        private readonly IGameData gameData;
        private readonly IServerManager serverManager;

        public ServerService(
            IGameData gameData,
            IServerManager serverManager,
            IHttpContextAccessor accessor,
            ISessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.serverManager = serverManager;
        }

        public void SendServerAnnouncement(string message, int milliSeconds)
        {
            serverManager.BroadcastMessageAsync(message, milliSeconds);
        }

        public void SendExpMultiplierEvent(int multiplier, string message, DateTime? startTime, DateTime endTime)
        {
            serverManager.SendExpMultiplierEventAsync(multiplier, message, startTime, endTime);
        }
    }
}
