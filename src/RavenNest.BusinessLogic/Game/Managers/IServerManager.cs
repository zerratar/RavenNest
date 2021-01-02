using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using System;
using RavenNest.DataModels;
using TwitchLib.Api.Helix.Models.Videos;

namespace RavenNest.BusinessLogic.Game
{
    public interface IServerManager
    {
        bool IncreaseGlobalExpMultiplier(DataModels.User user);
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

        public bool IncreaseGlobalExpMultiplier(DataModels.User user)
        {
            var activeEvent = gameData.GetActiveExpMultiplierEvent();
            if (activeEvent != null && !activeEvent.StartedByPlayer) return false;

            if (activeEvent == null)
            {
                activeEvent = new ExpMultiplierEvent
                {
                    Id = Guid.NewGuid(),
                    Multiplier = 2,
                    StartedByPlayer = true,
                    EventName = user.UserName,
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow.AddMinutes(15)
                };
                gameData.Add(activeEvent);
            }
            else
            {
                activeEvent.Multiplier++;
                activeEvent.EndTime = activeEvent.EndTime.AddMinutes(5);
            }

            activeEvent.EventName = user.UserName;
            return true;
        }

        public void SendExpMultiplierEventAsync(int multiplier, string message, DateTime? startTime, DateTime endTime)
        {
            var start = startTime ?? DateTime.UtcNow;
            var activeEvent = gameData.GetActiveExpMultiplierEvent();
            if (activeEvent != null)
            {
                if (activeEvent.StartedByPlayer && activeEvent.Multiplier >= multiplier)
                    return;

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
