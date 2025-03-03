using RavenNest.BusinessLogic.Data;
using System;

namespace RavenNest.BusinessLogic.Net
{
    public interface ITcpSocketApi : IDisposable
    {
        void Start();
    }
}
