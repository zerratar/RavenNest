using System;

namespace RavenNest.BusinessLogic.Game
{
    public interface IServerManager
    {
        bool IncreaseGlobalExpMultiplier(DataModels.User user);
        bool CanIncreaseGlobalExpMultiplier();
        void BroadcastMessageAsync(string message, int time);
        void SendExpMultiplierEventAsync(int multiplier, string message, DateTime? startTime, DateTime endTime);
    }
}
