using RavenNest.BusinessLogic.Data;
using System;

namespace RavenNest.BusinessLogic.Game
{
    public class PlayerIntegrityChecker : IIntegrityChecker
    {
        private readonly ILogger logger;
        private readonly IGameData gameData;
        private const float MaxSyncTimeDeltaSeconds = 15f;

        public PlayerIntegrityChecker(
            ILogger logger,
            IGameData gameData)
        {
            this.logger = logger;
            this.gameData = gameData;
        }

        public bool VerifyPlayer(Guid sessionId, Guid characterId, float syncTime)
        {
            var gameSession = gameData.GetSession(sessionId);
            if (gameSession == null)
            {

                logger.WriteError($"Player with ID {characterId} not part of session {sessionId}");
                return false;
            }

            var sessionState = gameData.GetSessionState(sessionId);
            var playerSessionState = gameData.GetCharacterSessionState(sessionId, characterId);
            if (playerSessionState.Compromised)
            {
                return false;
            }

            var syncDelta = syncTime - sessionState.SyncTime;
            var clientTime = gameSession.Started.AddSeconds(syncDelta);
            if (clientTime - DateTime.UtcNow > TimeSpan.FromSeconds(MaxSyncTimeDeltaSeconds))
            {
                logger.WriteError($"Player with ID {characterId} is compromised. CT: {clientTime}, SD: {syncDelta}");
                playerSessionState.Compromised = true;
                return false;
            }

            playerSessionState.SyncTime = syncTime;
            return true;
        }
    }
}