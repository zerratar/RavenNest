using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using System;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game
{
    public class ServerManager : IServerManager
    {
        public const int MaxExpMultiplier = 100;
        public const int ExpMultiplierStartTimeMinutes = 15;
        public const int ExpMultiplierLastTimeMinutes = 50;
        public const int ExpMultiplierMinutesPerScroll = 5;

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

        public bool CanIncreaseGlobalExpMultiplier()
        {
            return CanIncreaseGlobalExpMultiplier(1);
        }

        public bool CanIncreaseGlobalExpMultiplier(int count)
        {
            var activeEvent = gameData.GetActiveExpMultiplierEvent();
            if (activeEvent != null && !activeEvent.StartedByPlayer)
            {
                return false;
            }

            if (activeEvent != null && (activeEvent.Multiplier + count) > MaxExpMultiplier)
            {
                return false;
            }

            return true;
        }

        public int GetIncreasableGlobalExpAmount()
        {
            var activeEvent = gameData.GetActiveExpMultiplierEvent();
            if (activeEvent == null || (activeEvent != null && !activeEvent.StartedByPlayer))
            {
                return 0;
            }

            return MaxExpMultiplier - activeEvent.Multiplier;
        }

        public bool IncreaseGlobalExpMultiplier(DataModels.User user)
        {
            return IncreaseGlobalExpMultiplier(user, 1);
        }

        public bool IncreaseGlobalExpMultiplier(User user, int usageCount)
        {
            var activeEvent = gameData.GetActiveExpMultiplierEvent();
            if (activeEvent != null && !activeEvent.StartedByPlayer) return false;
            if (activeEvent == null)
            {
                activeEvent = new ExpMultiplierEvent
                {
                    Id = Guid.NewGuid(),
                    Multiplier = usageCount + 1,
                    StartedByPlayer = true,
                    EventName = user.UserName,
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow.AddMinutes(ExpMultiplierStartTimeMinutes)
                };
                gameData.Add(activeEvent);
            }
            else
            {
                activeEvent.Multiplier += usageCount;
                activeEvent.EndTime = activeEvent.EndTime.AddMinutes(activeEvent.Multiplier >= MaxExpMultiplier ? ExpMultiplierLastTimeMinutes : ExpMultiplierMinutesPerScroll);
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
