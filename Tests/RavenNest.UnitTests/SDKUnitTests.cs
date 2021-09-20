using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Serializers;

namespace RavenNest.UnitTests
{

    [TestClass]
    public class SDKUnitTests
    {

        [TestMethod]
        public Task TestSerialization()
        {
            ////var rawPacketSize = 4096;
            ////var bodySize = 1;
            //var rawData = new byte[] { 22, 117, 112, 100, 97, 116, 101, 95, 99, 104, 97, 114, 97, 99, 116, 101, 114, 95, 115, 116, 97, 116, 101, 20, 67, 104, 97, 114, 97, 99, 116, 101, 114, 83, 116, 97, 116, 101, 85, 112, 100, 97, 116, 101, 16, 0, 0, 0, 74, 107, 14, 129, 159, 165, 203, 73, 141, 89, 148, 41, 38, 162, 127, 177, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0 };
            //var payload = new byte[] { 1, };
            //var serializer = new BinarySerializer();
            //var gs = new GamePacketSerializer(null, serializer);

            //var packet = gs.Deserialize(rawData);


            //var targetType = typeof(RavenNest.BusinessLogic.Net.CharacterStateUpdate);
            //var data = serializer.Deserialize(payload, targetType);

            return Task.CompletedTask;
        }

        [TestMethod]
        public Task TestLogin()
        {
            //var client = new RavenNestClient(new ConsoleLogger(), new LocalRavenNestStreamSettings());
            //if (!await client.LoginAsync("zerratar", "zerratar"))
            //{
            //    Assert.Fail("Failed to login");
            //}
            return Task.CompletedTask;
        }

        [TestMethod]
        public Task TestLoginStartSession()
        {
            //var client = new RavenNestClient(new ConsoleLogger(), new LocalRavenNestStreamSettings());
            //if (await client.LoginAsync("zerratar", "zerratar"))
            //{
            //    if (!await client.StartSessionAsync(true))
            //    {
            //        Assert.Fail("Failed to start session");
            //    }

            //    if (!await client.EndSessionAsync())
            //    {
            //        Assert.Fail("Failed to end session");
            //    }

            //    return;
            //}

            //Assert.Fail("Failed to login");

            return Task.CompletedTask;
        }
    }
}
