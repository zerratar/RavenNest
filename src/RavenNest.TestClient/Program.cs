using System;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using RavenNest.SDK;
using RavenNest.SDK.Endpoints;

namespace RavenNest.TestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                var consoleKeyInfo = System.Console.ReadKey().Key;
                switch (consoleKeyInfo)
                {
                    case ConsoleKey.Q: return;
                    case ConsoleKey.Spacebar:
                        {
                            new ActualProgram().RunTest();
                        }
                        break;
                }
            }
        }
    }

    public class ActualProgram
    {
        public ActualProgram()
        {
            // LUL
        }

        public async Task RunTest()
        {
            var client = new RavenNestClient(new ConsoleLogger(), new LocalRavenNestStreamSettings());
            if (await client.LoginAsync("zerratar", "zerratar"))
            {
                if (!await client.StartSessionAsync(true))
                {
                    throw new Exception("Failed to start session");
                }

                var player = await client.PlayerJoinAsync("72424639", "zerratar");
                if (player == null)
                {
                    throw new Exception("Failed to generate character");
                }

                var expUpdate = await client.Players.UpdateExperienceAsync(player.UserId, new decimal[]
                {
                    1000000,
                    1000000,
                    1000000,
                    1000000,
                    1000000,
                    1000000,
                    1000000,
                    1000000,
                    1000000,
                    1000000
                });

                if (!expUpdate)
                {
                    throw new Exception("Failed to generate character");
                }



                if (!await client.EndSessionAsync())
                {
                    throw new Exception("Failed to end session");
                }

                return;
            }

            throw new Exception("Failed to login");
        }
    }
}
