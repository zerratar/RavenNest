namespace RavenNest.Twitch
{
    public class UserSubscriptionEvent
    {
        public string Channel { get; set; }
        public string UserId { get; set; }
        public string Platform { get; set; }
        public string ReceiverUserId { get; set; }
        public bool IsModerator { get; set; }
        public bool IsSubscriber { get; set; }
        public string UserName { get; set; }
        public string DisplayName { get; set; }
        public int Months { get; set; }
        public bool IsNew { get; set; }

        public UserSubscriptionEvent() { }
        public UserSubscriptionEvent(
            string platform,
            string channel,
            string userId,
            string userName,
            string displayName,
            string receiverUserId,
            bool isModerator,
            bool isSubscriber,
            int months,
            bool isNew)
        {
            Platform = platform;
            Channel = channel;
            UserId = userId;
            ReceiverUserId = receiverUserId;
            IsModerator = isModerator;
            IsSubscriber = isSubscriber;
            UserName = userName;
            DisplayName = displayName;
            Months = months;
            IsNew = isNew;
        }
    }
}
