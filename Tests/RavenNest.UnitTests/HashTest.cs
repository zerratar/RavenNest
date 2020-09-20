using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.BusinessLogic.Game;

namespace RavenNest.UnitTests
{
    [TestClass]
    public class HashTest
    {
        [TestMethod]
        public void GenerateHash1()
        {
            var hasher = new SecureHasher();
        }
    }
}
