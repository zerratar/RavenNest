﻿using System;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Game
{
    public class GameSessionPlayerCache
    {
        public DateTime Created { get; set; }
        public List<GameCachePlayerItem> Players { get; set; }
        public class GameCachePlayerItem
        {
            public TwitchPlayerInfo TwitchUser { get; set; }
            public System.Guid CharacterId { get; set; }
            public string NameTagHexColor { get; set; }
            public int CharacterIndex { get; set; }
            public class TwitchPlayerInfo
            {
                private static readonly HashSet<char> allowedCharacters = new HashSet<char>(new[] { '_', '=', 'q', 'w', 'e', 'r', 't', 'y', 'u', 'i', 'o', 'p', 'å', 'a', 's', 'd', 'f', 'g', 'h', 'j', 'k', 'l', 'ö', 'ä', 'z', 'x', 'c', 'v', 'b', 'n', 'm', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0' });
                public TwitchPlayerInfo() { }
                public TwitchPlayerInfo(
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
                    if (Identifier != null && Identifier.Length > 0)
                    {
                        var newIdentifier = "";
                        for (var i = 0; i < Identifier.Length; i++)
                        {
                            var c = Identifier[i];
                            if (allowedCharacters.Contains(char.ToLower(c)))
                            {
                                newIdentifier += c;
                            }
                        }
                        Identifier = newIdentifier;
                    }
                }
                public string Username { get; set; }
                public string UserId { get; set; }
                public string DisplayName { get; set; }
                public string Color { get; set; }
                public bool IsBroadcaster { get; set; }
                public bool IsModerator { get; set; }
                public bool IsSubscriber { get; set; }
                public bool IsVip { get; set; }
                public string Identifier { get; set; }
            }
        }
    }


}