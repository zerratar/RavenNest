using RavenNest.BusinessLogic.Data;
using System;

namespace RavenNest.BusinessLogic.Game
{
    public class PlayerIntegrityChecker : IIntegrityChecker
    {
        private readonly IGameData gameData;
        private const float MaxSyncTimeDeltaSeconds = 15f;

        public PlayerIntegrityChecker(IGameData gameData)
        {
            this.gameData = gameData;
        }

        public bool VerifyPlayer(Guid sessionId, Guid characterId, float syncTime)
        {
            var gameSession = gameData.GetSession(sessionId);
            if (gameSession == null)
            {
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
            if (DateTime.UtcNow - clientTime > TimeSpan.FromSeconds(MaxSyncTimeDeltaSeconds))
            {
                playerSessionState.Compromised = true;
                return false;
            }

            playerSessionState.SyncTime = syncTime;
            return true;
        }
    }
}