using Microsoft.Extensions.Options;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Net.DeltaTcpLib;
using RavenNest.Models;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DeltaServerTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Action<string> logger = (msg) =>
            {
                Console.WriteLine(msg);
            };
            var loggerProvider = new LoggerFactory();
            var sessionTokenProvider = new SessionTokenProvider();
            var deltaHandler = new DeltaHandler(logger);
            var settings = new RavenNest.BusinessLogic.AppSettings
            {
                TcpApiPort = 3921
            };

            var server = new DeltaServer(loggerProvider.CreateLogger<DeltaServer>(), settings, null, sessionTokenProvider, deltaHandler);
            server.Start();
            Console.WriteLine("Server started. Press any key to exit...");
            Console.ReadKey();
        }
    }

    public class DeltaHandler : IDeltaHandler
    {
        private Action<string> logger;

        public DeltaHandler(Action<string> logger)
        {
            this.logger = logger;
        }

        public void OnExperienceDelta(SessionToken session, IReadOnlyList<DeltaExperienceUpdate> deltas)
        {
            foreach (var delta in deltas)
            {
                logger($"Experience Delta: {delta.CharacterId} - {delta.DirtyMask}");

                foreach (var change in delta.Changes)
                {
                    logger($"Experience Change: [{change.Index}] {((Skill)change.Index)} - {change.Level} - {change.Experience}");
                }
            }
        }

        public void OnGameState(SessionToken session, GameStateRequest state)
        {
            logger($"Session ID: {session.SessionId}");
            logger($"Players Joined: {state.PlayerCount}");

            logger($"Dungeon Active: {state.Dungeon.IsActive}");
            if (state.Dungeon.IsActive)
            {
                logger($"Dungeon Boss Health: {state.Dungeon.CurrentBossHealth}/{state.Dungeon.MaxBossHealth}");
            }

            logger($"Raid Active: {state.Raid.IsActive}");
            if (state.Raid.IsActive)
                logger($"Raid Boss Health: {state.Raid.CurrentBossHealth}/{state.Raid.MaxBossHealth}");
        }

        public void OnPlayerStateDelta(SessionToken session, IReadOnlyList<CharacterStateDelta> deltas)
        {
            foreach (var delta in deltas)
            {
                logger($"Player State Delta: {delta.CharacterId}");
                logger($"Health Delta: {delta.Health}");
            }
        }
    }

    public class SessionTokenProvider : ISessionTokenProvider
    {
        public SessionToken Get(string sessionToken)
        {
            var json = Base64Decode(sessionToken);
            var token = JSON.Parse<SessionToken>(json);
            return token;
        }

        private static string Base64Decode(string str)
        {
            var data = System.Convert.FromBase64String(str);
            return Encoding.UTF8.GetString(data);
        }
    }
}
