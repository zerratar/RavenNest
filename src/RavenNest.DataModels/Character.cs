using System;

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
}
