using System;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Data
{
    public class BotStats
    {
        public int CommandsPerSecondsMax { get; set; }
        public int JoinedChannelsCount { get; set; }
        public int UserCount { get; set; }
        public int ConnectionCount { get; set; }
        public int SessionCount { get; set; }

        public long TotalCommandCount { get; set; }
        public double CommandsPerSecondsDelta { get; set; }

        public TimeSpan Uptime { get; set; }
        public DateTime LastSessionStarted { get; set; }
        public DateTime LastSessionEnded { get; set; }
        public DateTime Started { get; set; }

        public DateTime LastUpdated { get; set; }
        public Dictionary<string, object> Values { get; set; } = new Dictionary<string, object>();
        public TimeSpan TimeSinceUpdate => DateTime.UtcNow - LastUpdated;

        /// <summary>
        /// Twitch channels the bot is currently joined to.
        /// </summary>
        /// <remarks>
        /// The bot has always sent this; nothing here read it, so it was discarded on arrival. It
        /// is what lets a streamer be told whether the bot is actually in their channel rather than
        /// only whether the bot is alive somewhere.
        ///
        /// <para>
        /// This is every channel across every streamer, so it must never be rendered as a list.
        /// Only ever answer "is this one channel present" for the signed in user.
        /// </para>
        /// </remarks>
        public List<string> ListOfCurrentlyJoinedChannel { get; set; } = new List<string>();

        /// <summary>
        /// True when the bot has reported in recently enough to be considered online. The bot posts
        /// its details every few seconds, so a minute of silence means it is gone or cannot reach us.
        /// </summary>
        public bool IsOnline => LastUpdated != default && TimeSinceUpdate < TimeSpan.FromMinutes(1);

        /// <summary>
        /// Whether the bot is currently in the given channel. Case insensitive, because Twitch
        /// channel names are lower case while display names are not.
        /// </summary>
        public bool IsInChannel(string channelName)
        {
            if (string.IsNullOrEmpty(channelName) || ListOfCurrentlyJoinedChannel == null)
            {
                return false;
            }

            for (var i = 0; i < ListOfCurrentlyJoinedChannel.Count; i++)
            {
                if (string.Equals(ListOfCurrentlyJoinedChannel[i], channelName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
