using RavenNest.Kick;
using RavenNest.Models;
using RavenNest.Twitch;
using System;

namespace RavenNest.Sessions
{
    public class UserSessionInfo
    {
        public SessionInfo SessionInfo { get; set; }
        public TwitchRequests.TwitchUser TwitchUser { get; set; }
        public KickRequests.KickUser KickUser { get; set; }
    }
}
