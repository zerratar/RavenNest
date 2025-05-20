using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class UserLoyalty : Entity<UserLoyalty>
    {
        public const double MinExpForLevel = 1000;
        public const double ExpPerBit = 5;
        public const double ExpPerSubscription = 500;
        public const double ExpPerSecond = 0.2d; // 2h ish for first level.
        public const double SubscriberMultiplier = 5d;

        public const int PointsPerCheeredBit = 1;
        public const int PointsPerGiftedSub = 200;
        public const int PointsPerLevel = 100;

        [PersistentData] private Guid userId;
        [PersistentData] private Guid streamerUserId;
        [PersistentData] private Guid? rankId;
        [PersistentData] private long level;
        [PersistentData] private double experience;
        [PersistentData] private long giftedSubs;
        [PersistentData] private long cheeredBits;
        [PersistentData] private long points;
        [PersistentData] private bool isSubscriber;
        [PersistentData] private bool isModerator;
        [PersistentData] private bool isVip;
        [PersistentData] private string playtime;

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
