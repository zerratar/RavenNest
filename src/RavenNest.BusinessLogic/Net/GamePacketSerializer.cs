using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RavenNest.BusinessLogic.Net
{
    public class GamePacketSerializer : IGamePacketSerializer
    {
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

                var targetType = Assembly
                    .GetExecutingAssembly()
                    .GetTypes()
                    .FirstOrDefault(x => x.Name.Equals(packet.Type));

                var json = Decompress(payload);

                if (targetType == null)
                {
                    packet.Data = JsonConvert.DeserializeObject(json);
                    return packet;
                }

                packet.Data = JsonConvert.DeserializeObject(json, targetType);
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

                var json = JsonConvert.SerializeObject(packet.Data);
                var body = Compress(json);

                bw.Write(body.Length);
                bw.Write(body);

                return ms.ToArray();
            }
        }
        public static byte[] Compress(string text)
        {
            var bytes = Encoding.Unicode.GetBytes(text);
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(mso, CompressionMode.Compress))
                {
                    gs.Write(bytes, 0, bytes.Length);
                }
                return mso.ToArray();
            }
        }
        public static string Decompress(byte[] data)
        {
            // Read the last 4 bytes to get the length
            byte[] lengthBuffer = new byte[4];
            Array.Copy(data, data.Length - 4, lengthBuffer, 0, 4);
            int uncompressedSize = BitConverter.ToInt32(lengthBuffer, 0);

            var buffer = new byte[uncompressedSize];
            using (var ms = new MemoryStream(data))
            {
                using (var gzip = new GZipStream(ms, CompressionMode.Decompress))
                {
                    gzip.Read(buffer, 0, uncompressedSize);
                }
            }
            return Encoding.Unicode.GetString(buffer);
        }
    }

}
