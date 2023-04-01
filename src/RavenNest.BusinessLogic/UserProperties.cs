namespace RavenNest.BusinessLogic
{
    public static class UserProperties
    {
        public const string Twitch_PubSub = "twitch_pubsub";
        public const string Comment = "comment";

        public const string ChatMessageTransformation = "ChatMessageTransformation";
        public const string ChatBotLanguage = "ChatBotLanguage";

        public const string RavenfallTvShowName = "RavenfallTv_ShowName";
        public const string RavenfallTvShowDescription = "RavenfallTv_ShowDescription";
        public const string RavenfallTvShowLanguage = "RavenfallTv_ShowLanguage";
    }

    public enum ChatMessageTransformation : uint
    {
        Standard = 0,
        Personalize = 1,
        Translate = 2,
        TranslateAndPersonalize = 3
    }
}
