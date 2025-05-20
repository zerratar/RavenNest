// -----------------------------------------------------------------------------
// DeltaTcpLib.cs
// A reusable .NET Standard library for delta-based TCP messaging
// -----------------------------------------------------------------------------
using System;

namespace RavenNest.BusinessLogic.Net.DeltaTcpLib
{
    public struct DeltaExperienceUpdate
    {
        public Guid CharacterId;
        public uint DirtyMask;
        public SkillDelta[] Changes;
    }
}
