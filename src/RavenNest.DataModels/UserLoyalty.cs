using System;

namespace RavenNest.DataModels
{
    public class UserLoyalty : Entity<UserLoyalty>
    {
        public const double MinExpForLevel = 1000;
        public const double ExpPerBit = 5;
        public const double ExpPerSubscription = 500;
        public const double ExpPerSecond = 0.045d; // 2h ish for first level.
        public const double ActivityMultiplier = 10d;

        public const int PointsPerCheeredBit = 1;
        public const int PointsPerGiftedSub = 200;
        public const int PointsPerLevel = 100;

        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid userId; public Guid UserId { get => userId; set => Set(ref userId, value); }
        private Guid streamerUserId; public Guid StreamerUserId { get => streamerUserId; set => Set(ref streamerUserId, value); }
        private Guid? rankId; public Guid? RankId { get => rankId; set => Set(ref rankId, value); }
        private long level; public long Level { get => level; set => Set(ref level, value); }
        private double experience; public double Experience { get => experience; set => Set(ref experience, value); }
        private long giftedSubs; public long GiftedSubs { get => giftedSubs; set => Set(ref giftedSubs, value); }
        private long cheeredBits; public long CheeredBits { get => cheeredBits; set => Set(ref cheeredBits, value); }
        private long points; public long Points { get => points; set => Set(ref points, value); }
        private bool isSubscriber; public bool IsSubscriber { get => isSubscriber; set => Set(ref isSubscriber, value); }
        private bool isModerator; public bool IsModerator { get => isModerator; set => Set(ref isModerator, value); }
        private bool isVip; public bool IsVip { get => isVip; set => Set(ref isVip, value); }
        private string playtime; public string Playtime { get => playtime; set => Set(ref playtime, value); }

        private TimeSpan totalPlayTime;

        public void AddGiftedSubs(int newGiftedSubs)
        {
            this.GiftedSubs += newGiftedSubs;
            this.Points += PointsPerGiftedSub * newGiftedSubs;
            this.AddExperience(newGiftedSubs * ExpPerSubscription);
        }

        public void AddCheeredBits(int newCheeredBits)
        {
            this.CheeredBits += newCheeredBits;
            this.Points += PointsPerCheeredBit * newCheeredBits;
            this.AddExperience(newCheeredBits * ExpPerBit);
        }

        public void AddExperience(double amount)
        {
            this.Experience += amount;
            var expForLevel = GetExperienceForNextLevel();
            while (this.experience >= expForLevel)
            {
                this.Experience -= expForLevel;
                this.Level++;
                this.Points += GetLoyaltyPoints(this.Level);
                expForLevel = GetExperienceForNextLevel();
            }
        }


        public void AddPlayTime(TimeSpan time)
        {
            if (totalPlayTime == TimeSpan.Zero)
                TimeSpan.TryParse(this.playtime, out totalPlayTime);

            totalPlayTime += time;
            Playtime = totalPlayTime.ToString();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]

        public static int GetLoyaltyPoints(long level)
        {
            if (level <= 1)
            {
                return PointsPerLevel;
            }

            var percent = GetExpForLevel(level) / (double)MinExpForLevel;
            return (int)Math.Floor(50 + ((percent * PointsPerLevel) * 0.2d));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private double GetExperienceForNextLevel()
        {
            return GetExpForLevel(level);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static double GetExpForLevel(long level)
        {
            return (MinExpForLevel + ((level - 1) * 1.25d * (MinExpForLevel / 4d)));
        }
    }
}
