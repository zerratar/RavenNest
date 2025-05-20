// -----------------------------------------------------------------------------
// DeltaTcpLib.cs
// A reusable .NET Standard library for delta-based TCP messaging
// -----------------------------------------------------------------------------
using RavenNest.Models.TcpApi;

namespace RavenNest.BusinessLogic.Net.DeltaTcpLib
{
    // -------------------------------------------------------------------------
    // Game state models
    // -------------------------------------------------------------------------

    public class GameStateRequest
    {
        public int PlayerCount;
        public DungeonState Dungeon;
        public RaidState Raid;
    }
}
