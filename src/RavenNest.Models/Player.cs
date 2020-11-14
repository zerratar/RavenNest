using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public class PlayerJoinResult
    {
        public Player Player { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
    public class Player
    {
        public Guid Id { get; set; }
        public string UserId { get; set; }

        public string UserName { get; set; }

        public string Name { get; set; }

        public Statistics Statistics { get; set; }

        public SyntyAppearance Appearance { get; set; }

        public Resources Resources { get; set; }

        public Skills Skills { get; set; }

        public CharacterState State { get; set; }

        public IReadOnlyList<InventoryItem> InventoryItems { get; set; }

        public Clan Clan { get; set; }

        public bool IsAdmin { get; set; }

        public bool IsModerator { get; set; }

        public bool IsRejoin { get; set; }

        public Guid OriginUserId { get; set; }

        public int Revision { get; set; }
        public string Identifier { get; set; }
        public int CharacterIndex { get; set; }
    }

    public class PlayerFull
    {
        public Guid Id { get; set; }

        public string PasswordHash { get; set; }

        public string UserId { get; set; }

        public string UserName { get; set; }

        public string Name { get; set; }

        public Statistics Statistics { get; set; }

        public SyntyAppearance Appearance { get; set; }

        public Resources Resources { get; set; }

        public Skills Skills { get; set; }

        public CharacterState State { get; set; }

        public IReadOnlyList<InventoryItem> InventoryItems { get; set; }

        public Clan Clan { get; set; }

        public bool IsAdmin { get; set; }

        public bool IsModerator { get; set; }

        public Guid OriginUserId { get; set; }

        public int Revision { get; set; }

        public DateTime Created { get; set; }
        public string Identifier { get; set; }
        public int CharacterIndex { get; set; }
        public string SessionName { get; set; }
    }
}
