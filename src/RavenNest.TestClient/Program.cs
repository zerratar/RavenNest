using RavenNest.Models;
using System;
using System.Threading.Tasks;

namespace RavenNest.TestClient
{
    public class TestType
    {
        public Guid Id { get; set; }
        public string HelloWorld { get; set; }
        public DateTime OhNo { get; set; }
        public int[] IntArray { get; set; }
        public Vector3 v3 { get; set; }

        public Vector3 v32 { get; set; }
        public TestType2 Test2 { get; set; }
    }

    public class TestType2
    {
        public string Test { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var serializer = new BinarySerializer();
            var bool_a0 = serializer.Serialize(true);
            var bool_a1 = serializer.Deserialize(bool_a0, typeof(bool));

            var string_a0 = serializer.Serialize("Hello, World!");
            var string_a1 = serializer.Deserialize(string_a0, typeof(string));

            var dateTime_a0 = serializer.Serialize(DateTime.UtcNow);
            var dateTime_a1 = serializer.Deserialize(dateTime_a0, typeof(DateTime));

            var t = new TestType
            {
                HelloWorld = null,
                OhNo = DateTime.UtcNow,
                Id = Guid.NewGuid(),
                IntArray = new int[5],
                v32 = new Vector3()
                {
                    x = 123,
                    y = 456,
                    z = 789
                },
                Test2 = new TestType2
                {
                    Test = "test"
                }
            };

            var complex_a0 = serializer.Serialize(t);
            var complex_a1 = serializer.Deserialize(complex_a0, typeof(TestType));

            var v32 = new Vector3()
            {
                x = 123,
                y = 456,
                z = 789
            };
            var complex_b0 = serializer.Serialize(v32);
            var complex_b1 = serializer.Deserialize(complex_b0, typeof(Vector3));


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
        private readonly GameManager gameManager;
        private readonly DefaultLogger logger;
        private readonly LocalRavenNestStreamSettings settings;
        private readonly GamePacketSerializer packetSerializer;
        private readonly TokenProvider tokenProvider;
        private WebSocketEndpoint stream;

        public ActualProgram()
        {
            // LUL




            this.gameManager = new GameManager();
            this.logger = new DefaultLogger();
            this.settings = new LocalRavenNestStreamSettings();
            this.packetSerializer = new GamePacketSerializer(new BinarySerializer());
            this.tokenProvider = new TokenProvider();



        }

        public async Task RunTest()
        {
            //var username = "zerratar";
            //var password = "hahah this is not the password";

            //var requestBuilder = new WebApiRequestBuilderProvider(new LocalRavenNestStreamSettings(), tokenProvider);
            //var auth = new WebBasedAuthEndpoint(new DefaultLogger(), requestBuilder);
            //var res = await auth.AuthenticateAsync(username, password);

            var sessionToken = new SessionToken
            {
                //AuthToken = res.ToJson().Base64Encode()
                ExpiresUtc = DateTime.MaxValue,
                StartedUtc = DateTime.UtcNow,
                SessionId = Guid.Parse("2617DC87-241B-432B-B2F3-AD8A89E4670C")
            };

            tokenProvider.SetSessionToken(sessionToken);


            this.stream = new WebSocketEndpoint(
                gameManager,
                logger,
                settings,
                tokenProvider,
                packetSerializer);

            while (true)
            {
                logger.WriteLine("RunTestLoop");

                await this.stream.Update();

                await this.stream.SavePlayerAsync(new PlayerController
                {
                });


            }
        }
    }

    public class PlayerController : IPlayerController { }
    public class GameManager : IGameManager { }
}
