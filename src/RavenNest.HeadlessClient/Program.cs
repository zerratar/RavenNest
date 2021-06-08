using Newtonsoft.Json;
using RavenNest.HeadlessClient.Core;
using System;
using System.Configuration;
using System.Threading.Tasks;

namespace RavenNest.HeadlessClient
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var lastUpdate = DateTime.MinValue;

            var ioc = new IoC();
            using (new IoCContainerRegistry(ioc, TargetEnvironment.Production))
            using (var client = ioc.Resolve<IGameClient>())
            {
                if (!await client.AuthenticateAsync())
                {
                    return 1;
                }

                //if (!await client.BeginGameSessionAsync())
                //{
                //    return 2;
                //}

                while (true)
                {
                    var now = DateTime.Now;

                    if (now - lastUpdate > TimeSpan.FromMinutes(5))
                    {
                        await client.DownloadBackupAsync();

                        lastUpdate = DateTime.Now;
                    }

                    System.Threading.Thread.Sleep(1000);
                }

                //while (await client.WaitForGameEventsAsync(5, TimeSpan.FromSeconds(30)))
                //{
                //    System.Threading.Thread.Sleep(1);
                //}
            }
            return 0;
        }
    }
}
