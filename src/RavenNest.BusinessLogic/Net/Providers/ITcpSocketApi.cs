using RavenNest.BusinessLogic.Data;
using System;

namespace RavenNest.BusinessLogic.Net
{
    public interface ITcpSocketApi : IDisposable
    {
        IGameData GameData { get; }
        void Start();
    }
}
