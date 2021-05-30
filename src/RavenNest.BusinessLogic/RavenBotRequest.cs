using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic
{
    public class RavenBotRequest : IDisposable
    {
        private readonly RequestPath requestPath;
        private readonly TcpClient socket;

        private RavenBotRequest(RequestPath requestPath)
        {
            this.requestPath = requestPath;
            this.socket = new TcpClient();
        }

        public static RavenBotRequest Create(string requestUrl)
        {
            return new RavenBotRequest(RequestPath.Parse(requestUrl));
        }

        public async Task<bool> SendAsync(params string[] args)
        {
            try
            {
                await socket.ConnectAsync(this.requestPath.Host, this.requestPath.Port);

                using (var mem = new MemoryStream())
                using (var writer = new BinaryWriter(mem))
                {
                    var typeData = UTF8Encoding.UTF8.GetBytes(this.requestPath.Method);
                    writer.Write((byte)typeData.Length);
                    writer.Write(typeData);

                    var bytes = args.Select(UTF8Encoding.UTF8.GetBytes).ToArray();
                    var bodySize = (short)bytes.Sum(x => x.Length);

                    writer.Write(bodySize);

                    for (var i = 0; i < args.Length; ++i)
                    {
                        var data = bytes[i];
                        writer.Write((short)data.Length);
                        writer.Write(data);
                    }

                    await socket.GetStream().WriteAsync(mem.ToArray());
                }

                return true;
            }
            catch (Exception exc)
            {
                return false;
            }
        }
        public void Dispose()
        {
            try
            {
                if (this.socket.Connected)
                {
                    this.socket.Close();
                }

                this.socket.Dispose();
            }
            catch
            {
            }
        }

        private class RequestPath
        {
            public string Host { get; }
            public int Port { get; }
            public string Method { get; }

            public RequestPath(string host, int port, string method)
            {
                Host = host;
                Port = port;
                Method = method;
            }
            public static RequestPath Parse(string value)
            {
                try
                {
                    var hp = value.Split(':', '/');
                    return new RequestPath(hp[0], int.Parse(hp[1]), hp[2]);
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}
