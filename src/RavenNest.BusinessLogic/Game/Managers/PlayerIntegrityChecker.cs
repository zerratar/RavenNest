using RavenNest.BusinessLogic.Data;
using System;
using Microsoft.Extensions.Logging;

namespace RavenNest.BusinessLogic.Game
{
    public class PlayerIntegrityChecker : IIntegrityChecker
    {
        private readonly ILogger logger;
        private readonly GameData gameData;

        public PlayerIntegrityChecker(
            ILogger<PlayerIntegrityChecker> logger,
            GameData gameData)
        {
            this.logger = logger;
            this.gameData = gameData;
        }

        public bool VerifyPlayer(Guid sessionId, Guid characterId, float syncTime)
        {
            var gameSession = gameData.GetSession(sessionId);
            if (gameSession == null)
            {

                logger.LogError($"Player with ID {characterId} not part of session {sessionId}");
                return false;
            }

            var character = gameData.GetCharacter(characterId);
            if (gameSession.UserId != character.UserIdLock)
            {
                return false;
            }

            //var sessionState = gameData.GetSessionState(sessionId);
            var playerSessionState = gameData.GetCharacterSessionState(sessionId, characterId);
            if (playerSessionState.Compromised)
            {
                return false;
            }

            return true;

        }
    }
}
