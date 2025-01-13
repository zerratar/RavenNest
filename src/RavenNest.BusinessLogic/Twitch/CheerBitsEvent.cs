namespace RavenNest.Twitch
{
    public class CheerBitsEvent
    {
        public string Channel { get; set; }
        public string UserId { get; set; }
        public string Platform { get; set; }
        public string UserName { get; set; }
        public string DisplayName { get; set; }
        public bool IsModerator { get; set; }
        public bool IsSubscriber { get; set; }
        public bool IsVip { get; set; }
        public int Bits { get; set; }

        public CheerBitsEvent() { }
        public CheerBitsEvent(
            string platform,
            string channel,
            string userId,
            string userName,
            string displayName,
            bool isModerator,
            bool isSubscriber,
            bool isVip,
            int bits)
        {
            Platform = platform;
            Channel = channel;
            UserId = userId;
            UserName = userName;
            IsModerator = isModerator;
            IsSubscriber = isSubscriber;
            IsVip = isVip;
            DisplayName = displayName;
            Bits = bits;
        }
    }
}
