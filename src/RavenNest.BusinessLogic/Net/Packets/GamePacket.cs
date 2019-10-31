using System;

namespace RavenNest.BusinessLogic.Net
{
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