using System.Threading.Tasks;
using System.Collections.Concurrent;
using RavenNest.BusinessLogic.Net;

namespace RavenNest.TestClient
{
    public class WebSocketEndpoint : IWebSocketEndpoint
    {
        private ConcurrentDictionary<string, CharacterStateUpdate> lastSavedState
            = new ConcurrentDictionary<string, CharacterStateUpdate>();

        private readonly IGameServerConnection connection;
        private readonly IGameManager gameManager;
        public WebSocketEndpoint(
            IGameManager gameManager,
            ILogger logger,
            IAppSettings settings,
            ITokenProvider tokenProvider,
            IGamePacketSerializer packetSerializer)
        {
            this.connection = new WSGameServerConnection(
                logger,
                settings,
                tokenProvider,
                packetSerializer);

            this.connection.Register("game_event", new GameEventPacketHandler(gameManager));
            this.gameManager = gameManager;
        }

        public async Task<bool> UpdateAsync()
        {
            if (connection.IsReady)
            {
                return true;
            }

            if (connection.ReconnectRequired)
            {
                await Task.Delay(2000);
            }

            return await connection.CreateAsync();
        }

        public async Task<bool> SavePlayerAsync(IPlayerController player)
        {
            var characterUpdate = new CharacterStateUpdate(
                null,
                0,
                null,
                null,
                false,
                false,
                null,
                null,
                new Position
                {
                    X = 0f,
                    Y = 0f,
                    Z = 0f
                });

            if (lastSavedState.TryGetValue("test", out var lastUpdate))
            {
                if (!RequiresUpdate(lastUpdate, characterUpdate))
                {
                    return false;
                }
            }

            var response = await connection.SendAsync("update_character_state", characterUpdate);

            if (response == null)
            {
                // no response from server.
                return false;
            }

            if (response.TryGetValue<bool>(out var result))
            {
                if (result)
                {
                    lastSavedState["test"] = characterUpdate;
                    return true;
                }
            }

            return false;
        }

        private bool RequiresUpdate(CharacterStateUpdate oldState, CharacterStateUpdate newState)
        {
            return true;
            //if (oldState.Health != newState.Health) return true;
            //if (oldState.InArena != newState.InArena) return true;
            //if (oldState.InRaid != newState.InRaid) return true;
            //if (oldState.Island != newState.Island) return true;
            ////if (Math.Abs(oldState.Position.magnitude - newState.Position.magnitude) > 0.01) return true;
            //if (oldState.Task != newState.Task) return true;
            //if (oldState.TaskArgument != newState.TaskArgument) return true;
            //return oldState.DuelOpponent != newState.DuelOpponent;
        }
    }
}
