using Microsoft.EntityFrameworkCore;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game
{
    public interface IServerManager
    {
        void BroadcastMessageAsync(string message, int time);
        void SendExpMultiplierEventAsync(int multiplier, string message, DateTime? startTime, DateTime endTime);
    }

    public class ServerManager : IServerManager
    {
        private readonly IGameData gameData;
        public ServerManager(IGameData gameData)
        {
            this.gameData = gameData;
        }

        public void BroadcastMessageAsync(string message, int time)
        {
            var sessions = gameData.GetActiveSessions();

            foreach (var session in sessions)
            {
                var gameEvent = gameData.CreateSessionEvent(GameEventType.ServerMessage, session, new ServerMessage()
                {
                    Message = message,
                    Time = time
                });

                gameData.Add(gameEvent);
            }
        }

        public void SendExpMultiplierEventAsync(int multiplier, string message, DateTime? startTime, DateTime endTime)
        {
            var start = startTime ?? DateTime.UtcNow;
            var activeEvent = gameData.GetActiveExpMultiplierEvent();
            if (activeEvent != null)
            {
                if (start < activeEvent.EndTime)
                    activeEvent.EndTime = start;
            }
            var ev = new ExpMultiplierEvent();
            ev.Id = Guid.NewGuid();
            ev.EventName = message;
            ev.Multiplier = multiplier;
            ev.StartTime = start;
            ev.EndTime = endTime;
            gameData.Add(ev);
        }
    }
}
