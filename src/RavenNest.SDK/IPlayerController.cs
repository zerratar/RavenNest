using RavenNest.Models;
using RavenNest.SDK.Twitch;
using System;

namespace RavenNest.SDK
{
    public interface IPlayerController
    {
        string UserId { get; }
        Guid Id { get; }
        bool IsModerator { get; }
        bool IsSubscriber { get; }
        bool IsVip { get; }
        int BitsCheered { get; set; }
        int GiftedSubs { get; set; }
        TwitchPlayer TwitchUser { get; }
        string PlayerName { get; }

        PlayerState BuildPlayerState();
    }
}
