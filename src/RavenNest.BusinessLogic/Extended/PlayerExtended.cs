using RavenNest.DataModels;
using System;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Extended
{
    public class PlayerExtended
    {
        public string UserId { get; set; }

        public string UserName { get; set; }

        public string Name { get; set; }

        public Statistics Statistics { get; set; }

        public SyntyAppearance Appearance { get; set; }

        public Resources Resources { get; set; }

        public SkillsExtended Skills { get; set; }

        public CharacterState State { get; set; }

        public IReadOnlyList<InventoryItem> InventoryItems { get; set; }

        public Clan Clan { get; set; }

        public bool IsAdmin { get; set; }
        public bool IsModerator { get; set; }
        public Guid OriginUserId { get; set; }

        public int Revision { get; set; }
    }
}
