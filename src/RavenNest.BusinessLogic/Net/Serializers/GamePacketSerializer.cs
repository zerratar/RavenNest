using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RavenNest.BusinessLogic.Serializers;

namespace RavenNest.BusinessLogic.Net
{
    public class GamePacketSerializer : IGamePacketSerializer
    {
        private readonly IBinarySerializer binarySerializer;
        private readonly Dictionary<string, Type> loadedTypes;

        public GamePacketSerializer(IBinarySerializer binarySerializer)
        {
            this.binarySerializer = binarySerializer;
            this.loadedTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(x => x.GetTypes())
                .Where(x => x.IsPublic)
                .GroupBy(x => x.Name)
                .Select(x => x.FirstOrDefault())
                .ToDictionary(x => x.Name, x => x);
        }

        public GamePacket Deserialize(byte[] data)
        {
            return Deserialize(data, data.Length);
        }

        public GamePacket Deserialize(byte[] data, int length)
        {
            var packet = new GamePacket();
            using (var ms = new MemoryStream(data, 0, length))
            using (var br = new BinaryReader(ms))
            {
                packet.Id = br.ReadString();
                packet.Type = br.ReadString();
                packet.CorrelationId = new Guid(br.ReadBytes(br.ReadInt32()));

                var dataSize = br.ReadInt32();
                var payload = br.ReadBytes(dataSize);
                Type targetType = null;
                try
                {
                    packet.Data = loadedTypes.TryGetValue(packet.Type, out targetType)
                        ? binarySerializer.Deserialize(payload, targetType)
                        : payload;
                }
                catch (Exception exc)
                {
                    var hoverOverMe = GenerateDebugCode(length, data, dataSize, payload, targetType);
                    try
                    {
                        System.IO.File.WriteAllText(@"C:\Ravenfall\deserialize_" + targetType + ".cs", hoverOverMe);
                    }
                    catch { }
                    throw;
                }
            }
            return packet;
        }

        public byte[] Serialize(GamePacket packet)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(packet.Id);
                bw.Write(packet.Type);

                var correlationBytes = packet.CorrelationId.ToByteArray();
                bw.Write(correlationBytes.Length);
                bw.Write(correlationBytes);

                var body = binarySerializer.Serialize(packet.Data);
                bw.Write(body.Length);
                bw.Write(body);

                return ms.ToArray();
            }
        }

        private string GenerateDebugCode(int length, byte[] data, int dataSize, byte[] payload, Type targetType)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("var rawPacketSize = " + length + ";");
            sb.AppendLine("var bodySize = " + dataSize + ";");

            sb.Append("var rawData = new byte[] { ");
            foreach (var b in data)
                sb.Append(b.ToString() + ", ");
            sb.AppendLine("};");

            sb.Append("var payload = new byte[] { ");
            foreach (var b in payload)
                sb.Append(b.ToString() + ", ");
            sb.AppendLine("};");
            sb.AppendLine();
            sb.AppendLine("var targetType = typeof(" + targetType.FullName + ");");
            sb.AppendLine("var serializer = new BinarySerializer();");
            sb.AppendLine("var data = serializer.Deserialize(payload, targetType);");

            return sb.ToString();
        }
    }
}