// -----------------------------------------------------------------------------
// DeltaTcpLib.cs
// A reusable .NET Standard library for delta-based TCP messaging
// -----------------------------------------------------------------------------
namespace RavenNest.BusinessLogic.Net.DeltaTcpLib
{
    // -------------------------------------------------------------------------
    // Delta data structures
    // -------------------------------------------------------------------------
    public struct SkillDelta
    {
        public byte Index;
        public long Experience;
        public short Level;
    }
}
