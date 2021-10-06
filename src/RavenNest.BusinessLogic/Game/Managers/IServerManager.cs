using RavenNest.DataModels;
using System;

namespace RavenNest.BusinessLogic.Game
{
    public interface IServerManager
    {
        bool IncreaseGlobalExpMultiplier(DataModels.User user);
        bool IncreaseGlobalExpMultiplier(User user, int usageCount);
        bool CanIncreaseGlobalExpMultiplier();
        bool CanIncreaseGlobalExpMultiplier(int count);
        void BroadcastMessageAsync(string message, int time);
        void SendExpMultiplierEventAsync(int multiplier, string message, DateTime? startTime, DateTime endTime);
        int GetIncreasableGlobalExpAmount();
    }
}
