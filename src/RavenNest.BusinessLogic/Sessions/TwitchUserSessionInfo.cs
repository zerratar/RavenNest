using RavenNest.DataModels;
using RavenNest.Models;
using RavenNest.Twitch;
using System;
using System.Collections.Generic;

namespace RavenNest.Sessions
{
    public class TwitchUserSessionInfo
    {
        public SessionInfo SessionInfo { get; set; }
        public TwitchRequests.TwitchUser TwitchUser { get; set; }
    }
}
