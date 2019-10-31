using System;

namespace RavenNest.BusinessLogic.Net
{
    public interface IGamePacketManager
    {
        IGamePacketHandler Default { get; }

        bool TryGetPacketHandler(string id, out IGamePacketHandler packetHandler);
    }
}