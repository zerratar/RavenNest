// -----------------------------------------------------------------------------
// DeltaTcpLib.cs
// A reusable .NET Standard library for delta-based TCP messaging
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Net.DeltaTcpLib
{
    // -------------------------------------------------------------------------
    // Delta handler callbacks
    // -------------------------------------------------------------------------
    public interface IDeltaHandler
    {
        void OnExperienceDelta(SessionToken session, IReadOnlyList<DeltaExperienceUpdate> deltas);
        void OnPlayerStateDelta(SessionToken session, IReadOnlyList<CharacterStateDelta> deltas);
        void OnGameState(SessionToken session, GameStateRequest state);
    }
}
