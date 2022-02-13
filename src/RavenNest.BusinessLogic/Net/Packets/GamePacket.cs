using System;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Net
{
    public class GamePacketContainer : GamePacket
    {
        public IReadOnlyList<GamePacket> Packets { get; set; }
        public GamePacketContainer(IReadOnlyList<GamePacket> packets)
        {
            Id = "collection";
            Packets = packets;
            Data = packets;
            Type = nameof(GamePacketContainer);
        }
    }
    public class GamePacket
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public object Data { get; set; }
        public Guid CorrelationId { get; set; }

        public bool TryGetValue<T>(out T result)
        {
            if (Data is T res)
            {
                result = res;
                return true;
            }

            result = default;
            return false;
        }
    }
}
