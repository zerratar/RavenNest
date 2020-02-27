using System;

namespace RavenNest.BusinessLogic.Game
{
    public interface IIntegrityChecker
    {
        bool VerifyPlayer(Guid sessionId, Guid characterId, float playerSyncTime);
    }
}