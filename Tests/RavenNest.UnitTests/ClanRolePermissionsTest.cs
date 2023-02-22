using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.BusinessLogic.Game;

namespace RavenNest.UnitTests
{
    [TestClass]
    public class ClanRolePermissionsTest
    {
        [TestMethod]
        public void TestParsePermissions()
        {
            var officerPermissions = ClanManager.GenerateDefaultPermissions(3);            

            var officerPermissionsParsed = ClanRolePermissionsBuilder.Parse(officerPermissions);
            var permissions = ClanRolePermissionsBuilder.Parse("0000011001111");

        }
    }
}
