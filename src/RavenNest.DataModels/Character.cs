using System;
using System.Collections.Generic;

namespace RavenNest.DataModels
{
    public partial class Character : Entity<Character>
    {
        private Guid skillsId;
        private Guid syntyAppearanceId;
        private Guid userId;
        private Guid? prevUserIdLock;
        private Guid stateId;
        private Guid statisticsId;
        private Guid originUserId;
        private DateTime created;
        private string name;
        private string description;
        private Guid? userIdLock;
        private DateTime? lastUsed;
        private int characterIndex;
        private string identifier;
        private Guid? titleId;

        public Guid UserId { get => userId; set => Set(ref userId, value); }
        public Guid SyntyAppearanceId { get => syntyAppearanceId; set => Set(ref syntyAppearanceId, value); }
        public Guid SkillsId { get => skillsId; set => Set(ref skillsId, value); }
        [Obsolete] public Guid StatisticsId { get => statisticsId; set => Set(ref statisticsId, value); }
        public Guid StateId { get => stateId; set => Set(ref stateId, value); }
        public Guid OriginUserId { get => originUserId; set => Set(ref originUserId, value); }
        public DateTime Created { get => created; set => Set(ref created, value); }
        public string Name { get => name; set => Set(ref name, value); }
        public string Description { get => description; set => Set(ref description, value); }
        public Guid? TitleId { get => titleId; set => Set(ref titleId, value); }
        public Guid? UserIdLock { get => userIdLock; set => Set(ref userIdLock, value); }
        public Guid? PrevUserIdLock { get => prevUserIdLock; set => Set(ref prevUserIdLock, value); }
        public DateTime? LastUsed { get => lastUsed; set => Set(ref lastUsed, value); }
        public int CharacterIndex { get => characterIndex; set => Set(ref characterIndex, value); }
        public string Identifier { get => identifier; set => Set(ref identifier, value); }
    }
    public partial class Title : Entity<Title>
    {
        private string name;
        private string description;
        private int bonusType; // e.g., 1 = Attack, 2 = Defense, etc.
        private double bonusValue; // e.g., 0.1 for 10% bonus

        public string Name { get => name; set => Set(ref name, value); }
        public string Description { get => description; set => Set(ref description, value); }
        public int BonusType { get => bonusType; set => Set(ref bonusType, value); }
        public double BonusValue { get => bonusValue; set => Set(ref bonusValue, value); }
    }

    public enum QuestConditionType
    {
        KillMonsters = 1,
        CollectItems = 2,
        ReachLevel = 3,
        GatherResources = 4,
        CraftItems = 5,
        CompleteEvent = 6,
        ParticipateInRaid = 7,
        ParticipateInDungeon = 8
    }

    public partial class Quest : Entity<Quest>
    {
        private string title;
        private string description;
        private DateTime created;
        private DateTime? completed;

        public string Title { get => title; set => Set(ref title, value); }
        public string Description { get => description; set => Set(ref description, value); }
        public DateTime Created { get => created; set => Set(ref created, value); }
        public DateTime? Completed { get => completed; set => Set(ref completed, value); }

        public List<QuestStep> Steps { get; set; } = new List<QuestStep>();
        public List<QuestRequirement> Requirements { get; set; } = new List<QuestRequirement>();
        public List<QuestReward> Rewards { get; set; } = new List<QuestReward>();
        public List<Quest> ChainQuests { get; set; } = new List<Quest>();
    }

    public partial class QuestStep : Entity<QuestStep>
    {
        private Guid questId;
        private string description;
        private bool isOptional;
        private bool isCompleted;
        private int conditionType;
        private int? targetCount;
        private string itemId;
        private string resourceId;
        private int? resourceAmount;
        private int? itemIdToCraft;

        public Guid QuestId { get => questId; set => Set(ref questId, value); }
        public string Description { get => description; set => Set(ref description, value); }
        public bool IsOptional { get => isOptional; set => Set(ref isOptional, value); }
        public bool IsCompleted { get => isCompleted; set => Set(ref isCompleted, value); }
        public int ConditionType { get => conditionType; set => Set(ref conditionType, value); }
        public int? TargetCount { get => targetCount; set => Set(ref targetCount, value); }
        public string ItemId { get => itemId; set => Set(ref itemId, value); }
        public string ResourceId { get => resourceId; set => Set(ref resourceId, value); }
        public int? ResourceAmount { get => resourceAmount; set => Set(ref resourceAmount, value); }
        public int? ItemIdToCraft { get => itemIdToCraft; set => Set(ref itemIdToCraft, value); }
    }

    public partial class QuestRequirement : Entity<QuestRequirement>
    {
        private Guid questId;
        private int? requiredLevel;
        private string itemId;
        private int? itemAmount;
        private int conditionType;
        private string resourceId;
        private int? resourceAmount;
        private int? itemIdToCraft;

        public Guid QuestId { get => questId; set => Set(ref questId, value); }
        public int? RequiredLevel { get => requiredLevel; set => Set(ref requiredLevel, value); }
        public string ItemId { get => itemId; set => Set(ref itemId, value); }
        public int? ItemAmount { get => itemAmount; set => Set(ref itemAmount, value); }
        public int ConditionType { get => conditionType; set => Set(ref conditionType, value); }
        public string ResourceId { get => resourceId; set => Set(ref resourceId, value); }
        public int? ResourceAmount { get => resourceAmount; set => Set(ref resourceAmount, value); }
        public int? ItemIdToCraft { get => itemIdToCraft; set => Set(ref itemIdToCraft, value); }
    }

    public partial class QuestReward : Entity<QuestReward>
    {
        private Guid questId;
        private double? experience;
        private Guid? itemId;
        private int? itemAmount;
        private int? skillIndex;
        private Guid? titleId;

        public Guid QuestId { get => questId; set => Set(ref questId, value); }
        public double? Experience { get => experience; set => Set(ref experience, value); }
        public Guid? ItemId { get => itemId; set => Set(ref itemId, value); }
        public int? ItemAmount { get => itemAmount; set => Set(ref itemAmount, value); }
        public int? SkillIndex { get => skillIndex; set => Set(ref skillIndex, value); }
        public Guid? TitleId { get => titleId; set => Set(ref titleId, value); }
    }
}
