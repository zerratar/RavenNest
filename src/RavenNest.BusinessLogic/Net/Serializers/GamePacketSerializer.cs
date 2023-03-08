using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Serializers;

namespace RavenNest.BusinessLogic.Net
{
    public class GamePacketSerializer : IGamePacketSerializer
    {
        private readonly ILogger<GamePacketSerializer> logger;
        private readonly IBinarySerializer binarySerializer;
        private readonly Dictionary<string, Type> loadedTypes;

        public GamePacketSerializer(ILogger<GamePacketSerializer> logger, IBinarySerializer binarySerializer)
        {
            this.logger = logger;
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
            using (var ms = new MemoryStream(data, 0, length))
            using (var br = new BinaryReader(ms))
            {
                var packetId = br.ReadString();

                var dataSize = 0;
                var payload = Array.Empty<byte>();
                Type targetType = null;

                try
                {
                    if (packetId == "collection")
                    {
                        var packetCount = br.ReadInt32();
                        var packets = new List<GamePacket>();
                        for (var i = 0; i < packetCount; ++i)
                        {
                            var childPacket = new GamePacket();
                            childPacket.Id = br.ReadString();
                            ReadPacket(br, childPacket, out dataSize, out payload, out targetType);
                            packets.Add(childPacket);
                        }

                        return new GamePacketContainer(packets);
                    }
                    else
                    {
                        var packet = new GamePacket();
                        packet.Id = packetId;
                        ReadPacket(br, packet, out dataSize, out payload, out targetType);
                        return packet;
                    }
                }
                catch (Exception exc)
                {
                    logger?.LogError(exc.ToString());

                    var hoverOverMe = GenerateDebugCode(length, data, dataSize, payload, targetType);
                    try
                    {
                        System.IO.File.WriteAllText(@"deserialize_" + targetType + ".cs", hoverOverMe);
                    }
                    catch
                    {
                    }
                    throw;
                }
            }

            return null;
        }

        private void ReadPacket(BinaryReader br, GamePacket packet, out int dataSize, out byte[] payload, out Type targetType)
        {
            packet.Type = br.ReadString();
            packet.CorrelationId = new Guid(br.ReadBytes(br.ReadInt32()));

            dataSize = br.ReadInt32();
            payload = br.ReadBytes(dataSize);

            packet.Data = payload;

            if (loadedTypes.TryGetValue(packet.Type, out targetType))
            {
                packet.Data = binarySerializer.Deserialize(payload, targetType);
            }
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

            sb.AppendLine("var str = \"" + System.Text.Encoding.UTF8.GetString(payload) + "\"");
            sb.AppendLine("var targetType = typeof(" + targetType.FullName + ");");
            sb.AppendLine("var serializer = new " + binarySerializer.GetType().FullName + "();");
            sb.AppendLine("var data = serializer.Deserialize(payload, targetType);");

            return sb.ToString();
        }
    }
}
