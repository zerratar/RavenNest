using RavenNest.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MessagePack;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Serializers;

namespace RavenNest.TestClient
{
    [MessagePackObject] // only required for messagepack WutFace
    public class TestType
    {
        [Key(0)]
        public Guid Id { get; set; }
        [Key(1)]
        public string HelloWorld { get; set; }
        [Key(2)]
        public DateTime OhNo { get; set; }
        [Key(3)]
        public int[] IntArray { get; set; }
        [Key(4)]
        public Vector3 v3 { get; set; }
        [Key(5)]
        public Vector3 v32 { get; set; }
        [Key(6)]
        public TestType2 Test2 { get; set; }
        [Key(7)]
        public IReadOnlyList<string> Test3 { get; set; }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj == this) return true;
            if (obj is TestType other)
            {
                return other.Id == Id
                       && other.HelloWorld == HelloWorld
                       && IntArray.Length == other.IntArray.Length
                       && Enumerable.SequenceEqual(IntArray, other.IntArray)
                       && v3.magnitude == other.v3.magnitude
                       && v32.magnitude == other.v32.magnitude
                       && Test2?.Test == other.Test2?.Test;
            }

            return false;
        }
    }

    [MessagePackObject] // only required for messagepack WutFace
    public class TestType2
    {
        [Key(0)]
        public string Test { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            //TestSerializer(new MsgPackSerializer());
            TestSerializer(new BinarySerializer());
            TestSerializer(new JsonSerializer());
            TestSerializer(new CompressedJsonSerializer());

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

        private static void TestSerializer(IBinarySerializer serializer)
        {
            void Add(ref (int, long) self, (int, long) other)
            {
                self = (self.Item1 + other.Item1, self.Item2 + other.Item2);
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Testing serializer: " + serializer.GetType().FullName);
            Console.ResetColor();

            (int total, long elapsed) total = (0, 0);
            Add(ref total, TestSerializer(serializer, new List<int> { 12345 }));
            Add(ref total, TestSerializer(serializer, new Dictionary<string, string> { { "hello", "world" }, { "hello1", "world" }, { "hello2", "world" } }));
            Add(ref total, TestSerializer(serializer, new List<string> { "hello", "world", "world2", "world3" }));
            Add(ref total, TestSerializer(serializer, true));
            Add(ref total, TestSerializer(serializer, "Hello, World!"));
            Add(ref total, TestSerializer(serializer, DateTime.UtcNow));
            Add(ref total, TestSerializer(serializer, new TestType
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
            }));

            Add(ref total, TestSerializer(serializer, new Vector3
            {
                x = 123,
                y = 456,
                z = 789
            }));

            Console.Write("------------ TOTAL: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(total.total + " bytes ");
            Console.ResetColor();

            Console.Write("TIME: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(total.elapsed + " ticks");
            Console.WriteLine();
        }

        private static (int, long) TestSerializer(IBinarySerializer serializer, object model)
        {
            var sw = new Stopwatch();
            sw.Start();

            var serialized = serializer.Serialize(model);
            var deserialized = serializer.Deserialize(serialized, model.GetType());

            sw.Stop();

            Console.Write("Type: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(model.GetType().Name + " ");
            Console.ResetColor();

            Console.Write("Size: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(serialized.Length + " ");
            Console.ResetColor();


            Console.Write("Time: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(sw.ElapsedTicks + " ticks ");
            Console.ResetColor();


            Console.Write("Result: ");
            var ok = Object.Equals(model, deserialized) || model == deserialized || model.ToJson().Equals(deserialized.ToJson());
            if (ok)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("OK");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed");
            }
            Console.ResetColor();

            return (serialized.Length, sw.ElapsedTicks);
        }
    }

    public class LocalRavenNestStreamSettings : IAppSettings
    {
        public string ApiEndpoint => "https://localhost:5001/api/";
        public string WebSocketEndpoint => "wss://localhost:5001/api/stream";
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
                if (await this.stream.UpdateAsync())
                {
                    Console.WriteLine("Saving player state.");
                    var result = await this.stream.SavePlayerAsync(new PlayerController());
                    Console.WriteLine("Server Responded with: " + result);
                }
                else
                {
                    Console.WriteLine("Trying to reconnect to the server...");
                }
            }
        }
    }

    public class PlayerController : IPlayerController { }
    public class GameManager : IGameManager { }
}
