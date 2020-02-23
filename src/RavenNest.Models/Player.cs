using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public class Player
    {
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

        public bool Local { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsModerator { get; set; }
        public Guid OriginUserId { get; set; }

        public int Revision { get; set; }
    }
}