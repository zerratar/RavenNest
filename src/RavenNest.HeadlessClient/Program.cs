using MessagePack;
using Newtonsoft.Json;
using RavenNest.BusinessLogic.Net;
using RavenNest.HeadlessClient.Core;
using RavenNest.Models;
using RavenNest.Models.TcpApi;
using System;
using System.Configuration;
using System.Text;
using System.Threading.Tasks;
using Telepathy;

namespace RavenNest.HeadlessClient
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var lastUpdate = DateTime.MinValue;


            var ioc = new IoC();
            using (new IoCContainerRegistry(ioc, TargetEnvironment.Local)) //Production
            //{
            //    var tcpClient = ioc.Resolve<TcpApiClient>();
            //    tcpClient.Connect();
            //    while (true)
            //    {
            //        //lastUpdate = await DownloadBackupAsync(lastUpdate, client);
            //        tcpClient.Update();
            //        if (!tcpClient.Connected)
            //        {
            //            tcpClient.Connect();
            //            System.Threading.Thread.Sleep(3000);
            //        }
            //        System.Threading.Thread.Sleep(1000 / 60);
            //    }
            //}


            using (var client = ioc.Resolve<IGameClient>())
            {
                if (!await client.AuthenticateAsync())
                {
                    return 1;
                }

                if (!await client.BeginGameSessionAsync())
                {
                    return 2;
                }

                var sentUpdateTest = false;

                var tcpClient = ioc.Resolve<TcpApiClient>();
                tcpClient.Connect(client.SessionToken);
                while (true)
                {
                    //lastUpdate = await DownloadBackupAsync(lastUpdate, client);
                    tcpClient.Update();
                    if (!tcpClient.Connected)
                    {
                        tcpClient.Connect(client.SessionToken);
                        System.Threading.Thread.Sleep(3000);
                        continue;
                    }

                    if (!sentUpdateTest)
                    {
                        var test = new CharacterUpdate()
                        {
                            CharacterId = Guid.NewGuid(),
                            Skills = new System.Collections.Generic.Dictionary<string, SkillUpdate>
                            {
                                ["Strength"] = new SkillUpdate { Level = 1, Experience = 0 },
                                ["Health"] = new SkillUpdate { Level = 10, Experience = 1000 },
                            }
                        };

                        tcpClient.Send(test);
                        sentUpdateTest = true;
                    }

                    System.Threading.Thread.Sleep(1000 / 60);
                }
            }

            return 0;
        }

        private static async Task<DateTime> DownloadBackupAsync(DateTime lastUpdate, IGameClient client)
        {
            var now = DateTime.Now;
            if (now - lastUpdate > TimeSpan.FromMinutes(5))
            {
                await client.DownloadBackupAsync();
                lastUpdate = DateTime.Now;
            }

            return lastUpdate;
        }
    }

    public class TcpApiClient : IDisposable
    {
        public const int MaxMessageSize = 16 * 1024;
        public const int ServerPort = 3920;

        private SessionToken sessionToken;
        private Client client;
        private bool connecting;

        public bool Connected => client?.Connected ?? false;

        public void Connect(SessionToken sessionToken)
        {
            if (connecting)
                return;

            this.sessionToken = sessionToken;
            client = new Telepathy.Client(MaxMessageSize);
            client.OnConnected = OnClientConnected;
            client.OnDisconnected = OnClientDisconnected;
            client.OnData = OnData;
            client.Connect("127.0.0.1", ServerPort);
        }

        public void Dispose()
        {
            if (client != null && client.Connected)
            {
                client.Disconnect();
            }

            client = null;
        }

        public void Update()
        {
            client.Tick(100);
        }
        public bool Send(object data)
        {
            var packetData = MessagePackSerializer.Serialize(data, MessagePack.Resolvers.ContractlessStandardResolver.Options);
            return client.Send(packetData);
        }

        private void OnData(ArraySegment<byte> obj)
        {
            if (connecting && obj != null && obj.Count > 0)
                connecting = false;
        }

        private void OnClientDisconnected()
        {
            connecting = false;
        }

        private void OnClientConnected()
        {
            connecting = false;

            var packetData = MessagePackSerializer.Serialize(new AuthenticationRequest()
            {
                SessionToken = Base64Encode(Newtonsoft.Json.JsonConvert.SerializeObject(sessionToken))
            }, MessagePack.Resolvers.ContractlessStandardResolver.Options);

            client.Send(packetData);
        }
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
    }
}
