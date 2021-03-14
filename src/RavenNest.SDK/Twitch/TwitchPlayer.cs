﻿using System;

namespace RavenNest.SDK.Twitch
{
    public class TwitchPlayer
    {
        public TwitchPlayer(
            string userId,
            string username,
            string displayName,
            string color,
            bool isBroadcaster,
            bool isModerator,
            bool isSubscriber,
            bool isVip,
            string identifier)
        {
            if (string.IsNullOrEmpty(username)) throw new ArgumentNullException(nameof(username));
            Username = username.StartsWith("@") ? username.Substring(1) : username;
            UserId = userId;
            DisplayName = displayName;
            Color = color;
            IsBroadcaster = isBroadcaster;
            IsModerator = isModerator;
            IsSubscriber = isSubscriber;
            IsVip = isVip;
            Identifier = identifier;
        }

        public string Username { get; }
        public string UserId { get; }
        public string DisplayName { get; }
        public string Color { get; }
        public bool IsBroadcaster { get; }
        public bool IsModerator { get; }
        public bool IsSubscriber { get; }
        public bool IsVip { get; }
        public string Identifier { get; }
    }
}
