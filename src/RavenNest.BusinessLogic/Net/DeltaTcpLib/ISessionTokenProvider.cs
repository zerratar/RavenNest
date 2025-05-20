// -----------------------------------------------------------------------------
// DeltaTcpLib.cs
// A reusable .NET Standard library for delta-based TCP messaging
// -----------------------------------------------------------------------------
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Net.DeltaTcpLib
{
    public interface ISessionTokenProvider
    {
        SessionToken Get(string rawToken);
    }
}
