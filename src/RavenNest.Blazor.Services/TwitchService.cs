using RavenNest.BusinessLogic.Data;
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
                stream.TwitchUserName = user.UserName;
                stream.UserTitle = "";
                stream.Uptime = DateTime.UtcNow - session.Started;
                stream.PlayerCount = gameData.GetActiveSessionCharacters(session).Count;
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
    }
}
