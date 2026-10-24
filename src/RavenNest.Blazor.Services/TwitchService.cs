using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Blazor.Services
{
    public class TwitchService
    {
        private readonly GameData gameData;

        public TwitchService(GameData gameData)
        {
            this.gameData = gameData;
        }

        public IReadOnlyList<TwitchStream> GetTwitchStreams()
        {
            var ran = new Random();
            var output = new List<TwitchStream>();
            var sessions = gameData.GetActiveSessions();
            foreach (var session in sessions)
            {
                var stream = new TwitchStream();
                var user = gameData.GetUser(session.UserId);
                if (user == null)
                {
                    continue;
                }

                stream.TwitchUserName = user.UserName;
                stream.UserTitle = "";
                stream.Uptime = DateTime.UtcNow - session.Started;
                stream.PlayerCount = gameData.GetActiveSessionCharacters(session).Count;
                (stream.ClientVersion, stream.IsLatestVersion) = ClientVersionInfo.Of(gameData, session);
                stream.LatestVersion = ClientVersionInfo.Latest(gameData);
                output.Add(stream);
            }
            return output.OrderBy(x => ran.Next()).ToList();
        }
    }

    public class TwitchStream
    {
        public string TwitchUserName { get; set; }
        public string UserTitle { get; set; }
        public int PlayerCount { get; set; }
        public TimeSpan Uptime { get; set; }
        public bool IsVisible { get; set; }

        /// <summary>The game version the stream runs, as the game reported it; empty when unknown.</summary>
        public string ClientVersion { get; set; }

        /// <summary>Whether that is the newest release (or newer), so viewers know where the new things are.</summary>
        public bool IsLatestVersion { get; set; }

        public string LatestVersion { get; set; }
    }

    /// <summary>The game version a stream runs, for the stream cards and the towns list.</summary>
    public static class ClientVersionInfo
    {
        public static (string Version, bool IsLatest) Of(GameData gameData, DataModels.GameSession session)
        {
            var state = session != null ? gameData.GetSessionState(session.Id) : null;
            var version = state?.ClientVersion;
            if (string.IsNullOrWhiteSpace(version)) return (null, false);
            version = version.Trim();
            return (version.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? version : "v" + version, gameData.IsExpectedVersion(version));
        }

        /// <summary>The newest release, for the badge's tooltip.</summary>
        public static string Latest(GameData gameData)
        {
            var version = gameData.Client?.ClientVersion;
            return string.IsNullOrWhiteSpace(version) ? null : (version.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? version : "v" + version);
        }
    }
}
