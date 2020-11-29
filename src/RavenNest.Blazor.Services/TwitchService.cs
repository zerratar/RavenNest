using RavenNest.BusinessLogic.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class TwitchService
    {
        private readonly IGameData gameData;

        public TwitchService(IGameData gameData)
        {
            this.gameData = gameData;
        }

        public IReadOnlyList<TwitchStream> GetTwitchStreams()
        {
            var output = new List<TwitchStream>();
            var sessions = gameData.GetActiveSessions();
            foreach (var session in sessions)
            {
                var stream = new TwitchStream();
                var user = gameData.GetUser(session.UserId);
                stream.TwitchUserName = user.UserName;
                stream.UserTitle = "";
                stream.Uptime = DateTime.UtcNow - session.Started;
                stream.PlayerCount = gameData.GetSessionCharacters(session).Count;
                output.Add(stream);
            }
            return output;
        }
    }

    public class TwitchStream
    {
        public string TwitchUserName { get; set; }
        public string UserTitle { get; set; }
        public int PlayerCount { get; set; }
        public TimeSpan Uptime { get; set; }
    }
}
