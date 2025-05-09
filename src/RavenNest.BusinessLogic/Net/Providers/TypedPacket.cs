using System;
using MessagePack;

namespace RavenNest.BusinessLogic.Net
{
    /// <summary>
    /// Lightweight “envelope” containing the message type, session token, and
    /// the actual payload in a serialized form. This avoids multiple deserialization attempts.
    /// </summary>
    [MessagePackObject]
    public class TypedPacket
    {
        [Key(0)]
        public TcpMessageType MessageType { get; set; }

        // We store the session token at the “envelope” level, so we can validate
        // before fully deserializing the payload (if you want).
        [Key(1)]
        public string SessionToken { get; set; }

        [Key(2)]
        public DateTimeOffset Timestamp { get; set; }

        // The “raw” payload. We’ll decode this into the correct struct/class.
        [Key(3)]
        public byte[] Payload { get; set; }


        [IgnoreMember]
        public object Object { get; set; }

        internal T Deserialize<T>()
        {
            try
            {
                if (Object != null && Object is T o)
                {
                    return o;
                }
            }
            catch { }

            try
            {
                return MessagePackSerializer.Deserialize<T>(
                    Payload,
                    MessagePack.Resolvers.ContractlessStandardResolver.Options
                );
            }
            catch (Exception ex)
            {
                // ignored
            }
            return default;
        }
    }
}
