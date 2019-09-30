using System;
using System.Collections.Generic;

namespace RavenNest.DataModels
{
    public partial class Character
    {
        public Character()
        {
            //CharacterSession = new HashSet<CharacterSession>();
            InventoryItem = new HashSet<InventoryItem>();
        }

        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid AppearanceId { get; set; }
        public Guid? SyntyAppearanceId { get; set; }
        public Guid SkillsId { get; set; }
        public Guid StatisticsId { get; set; }
        public Guid ResourcesId { get; set; }
        public Guid? StateId { get; set; }
        public bool Local { get; set; }
        public Guid OriginUserId { get; set; }
        public DateTime Created { get; set; }
        public string Name { get; set; }
        public int? Revision { get; set; }
        public Guid? UserIdLock { get; set; }
        public DateTime? LastUsed { get; set; }
        public Statistics Statistics { get; set; }
        public Appearance Appearance { get; set; }

        public SyntyAppearance SyntyAppearance { get; set; }

        public CharacterState State { get; set; }

        public User OriginUser { get; set; }
        public Resources Resources { get; set; }
        public Skills Skills { get; set; }
        public User User { get; set; }
        //public ICollection<CharacterSession> CharacterSession { get; set; }
        public ICollection<InventoryItem> InventoryItem { get; set; }
        public ICollection<MarketItem> MarketItem { get; set; }
    }
}