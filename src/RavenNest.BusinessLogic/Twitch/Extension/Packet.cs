using System;
using System.Linq;
using System.Net.WebSockets;

namespace RavenNest.BusinessLogic.Twitch.Extension
{
    public class Packet
    {
        private readonly IExtensionPacketDataSerializer packetDataSerializer;

        public string Header;

        public byte[] Data;
        public int Size;
        public WebSocketMessageType MessageType;
        public bool EndOfMessage;

        public Packet(IExtensionPacketDataSerializer packetDataSerializer, string header, byte[] data, int size, WebSocketMessageType messageType, bool endOfMessage)
        {
            this.packetDataSerializer = packetDataSerializer;
            this.Data = data;
            this.Size = size;
            this.MessageType = messageType;
            this.EndOfMessage = endOfMessage;
            this.Header = header;
        }

        public Packet Append(Packet packet)
        {
            var bytes = this.Data.Take(this.Size).ToList();
            bytes.AddRange(packet.Data.Take(packet.Size));
            var newSize = Size + packet.Size;
            var messageType = this.MessageType;
            var endOfMessage = packet.EndOfMessage;
            return new Packet(packetDataSerializer, Header, bytes.ToArray(), newSize, messageType, endOfMessage);
        }

        public T Serialize<T>()
        {
            return packetDataSerializer.Serialize<T>(this);
        }

        public bool Is<T>()
        {
            return this.Header.Equals(typeof(T).Name, StringComparison.OrdinalIgnoreCase);
        }

        public bool Is<T>(out T item)
        {
            item = default(T);
            if (!Is<T>())
            {
                return false;
            }

            try
            {
                item = Serialize<T>();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public ArraySegment<byte> Build()
        {
            return new ArraySegment<byte>(packetDataSerializer.Deserialize(this));
        }
    }
}
