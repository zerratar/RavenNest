﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Models
{
    public class Player
    {
        public Player() { }
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string UserName { get; set; }
        public string Name { get; set; }

        public Statistics Statistics { get; set; }
        public SyntyAppearance Appearance { get; set; }
        public Resources Resources { get; set; }
        public Skills Skills { get; set; }
        public CharacterState State { get; set; }
        public Guid? ActiveBattlePet { get; set; }
        public IReadOnlyList<InventoryItem> InventoryItems { get; set; }
        public IReadOnlyList<BattlePet> BattlePets { get; set; }
        public IReadOnlyList<CharacterStatusEffect> StatusEffects { get; set; }
        public Clan Clan { get; set; }
        public ClanRole ClanRole { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsModerator { get; set; }
        public bool IsRejoin { get; set; }
        public Guid OriginUserId { get; set; }
        public string Identifier { get; set; }
        public string FullBodySkin { get; set; }
        public int CharacterIndex { get; set; }
        public int PatreonTier { get; set; }
        public bool IsHiddenInHighscore { get; set; }
        public IReadOnlyList<AuthServiceConnection> Connections { get; set; }
        public AuthServiceConnection Twitch => Connections.FirstOrDefault(x => x.Platform.Equals("twitch", StringComparison.OrdinalIgnoreCase));
        public AuthServiceConnection Discord => Connections.FirstOrDefault(x => x.Platform.Equals("discord", StringComparison.OrdinalIgnoreCase));
        public AuthServiceConnection YouTube => Connections.FirstOrDefault(x => x.Platform.Equals("youtube", StringComparison.OrdinalIgnoreCase));
        public AuthServiceConnection Kick => Connections.FirstOrDefault(x => x.Platform.Equals("kick", StringComparison.OrdinalIgnoreCase));
    }

    public class AuthServiceConnection
    {
        public AuthServiceConnection() { }
        public string Platform { get; set; }
        public string PlatformId { get; set; }
        public string PlatformUserName { get; set; }
    }
}
