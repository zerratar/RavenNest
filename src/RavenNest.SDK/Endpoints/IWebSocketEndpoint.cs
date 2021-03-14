using RavenNest.SDK.EventSystem;
using RavenNest.SDK.Twitch;
using System;
using System.Threading.Tasks;

namespace RavenNest.SDK.Endpoints
{
    public interface IWebSocketEndpoint
    {
        bool IsReady { get; }
        bool ForceReconnecting { get; }
        Task<bool> UpdateAsync();
        Task<bool> SavePlayerStateAsync(IPlayerController player);
        Task<bool> SavePlayerSkillsAsync(IPlayerController player);
        void SendPlayerLoyaltyData(IPlayerController player);
        void SendPlayerLoyaltyData(TwitchSubscription data);
        void SendPlayerLoyaltyData(TwitchCheer data);
        void Close();
        void Reconnect();
        Task UpdatePlayerEventStatsAsync(EventTriggerSystem.SysEventStats e);
        void SyncTimeAsync(TimeSpan delta, DateTime localTime, DateTime serverTime);
    }
}
