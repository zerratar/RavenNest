using System;
using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.Sessions;

namespace RavenNest.Blazor.Services
{
    /// <summary>
    /// Builds the bot status shown to a streamer on their own dashboard.
    /// </summary>
    /// <remarks>
    /// Everything here is scoped to the signed in user. The underlying <see cref="BotStats"/> is
    /// global and includes every streamer's channel, so nothing on this service returns a list or
    /// a count that would leak another streamer's state.
    /// </remarks>
    public class BotService : RavenNestService
    {
        private readonly GameData gameData;

        public BotService(
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider,
            GameData gameData)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
        }

        public BotStatus GetBotStatus()
        {
            var status = new BotStatus();
            var session = GetSession();

            if (session == null || !session.Authenticated)
            {
                return status;
            }

            var bot = gameData.Bot;
            status.BotOnline = bot != null && bot.IsOnline;
            status.BotUptime = bot?.Uptime ?? TimeSpan.Zero;
            status.BotLastReported = bot?.LastUpdated ?? default;

            var user = gameData.GetUser(session.UserId);
            if (user == null)
            {
                return status;
            }

            status.AccountName = user.UserName;
            status.DisplayName = user.DisplayName;

            var twitch = gameData.GetUserAccess(user.Id, "twitch");
            status.TwitchLogin = twitch?.PlatformUsername;
            status.HasTwitchAccount = twitch != null;

            // The channel the bot joins comes from the account name, so that is what to check
            // against rather than the Twitch login. When the two disagree the bot is in the wrong
            // place, and saying which name it used is the whole point of showing this.
            status.ExpectedChannel = user.UserName;
            status.BotInChannel = bot != null && bot.IsInChannel(status.ExpectedChannel);

            // A rename leaves these out of step until something refreshes them. Surfacing it here
            // turns a confusing "the bot stopped working" into something the streamer can see and
            // act on.
            status.NameMismatch =
                !string.IsNullOrEmpty(status.TwitchLogin) &&
                !string.IsNullOrEmpty(status.AccountName) &&
                !string.Equals(status.AccountName, status.TwitchLogin, StringComparison.OrdinalIgnoreCase);

            // updateSession: false matters. The default stamps Updated on the session, and this page
            // polls. An open dashboard tab would keep a dead session past the 30 minute expiry in
            // BeginSessionAsync and dirty the entity every poll. Reading a status must not extend it.
            var gameSession = gameData.GetSessionByUserId(user.Id, updateSession: false);
            if (gameSession != null)
            {
                status.HasActiveGameSession = true;
                status.GameSessionStarted = gameSession.Started;
            }

            return status;
        }
    }

    /// <summary>
    /// What one streamer is allowed to know about the bot.
    /// </summary>
    public class BotStatus
    {
        /// <summary>Whether the centralized bot is reporting in at all.</summary>
        public bool BotOnline { get; set; }
        public TimeSpan BotUptime { get; set; }
        public DateTime BotLastReported { get; set; }

        /// <summary>The channel the bot should be in, which is the Ravenfall account name.</summary>
        public string ExpectedChannel { get; set; }

        /// <summary>Whether the bot is actually in that channel right now.</summary>
        public bool BotInChannel { get; set; }

        public string AccountName { get; set; }
        public string DisplayName { get; set; }
        public string TwitchLogin { get; set; }
        public bool HasTwitchAccount { get; set; }

        /// <summary>The account name and the Twitch login disagree, usually after a rename.</summary>
        public bool NameMismatch { get; set; }

        public bool HasActiveGameSession { get; set; }
        public DateTime? GameSessionStarted { get; set; }

        /// <summary>
        /// True when the bot is up and in the right channel. Used to decide whether to lead with
        /// reassurance or with the problem.
        /// </summary>
        public bool AllGood => BotOnline && BotInChannel && !NameMismatch;
    }
}
