using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Combat;

namespace RavenNest.UnitTests
{
    /// <summary>The Legendaries on the server (the game's docs/legendary-design.md): the same items as the Dark Fortress pieces, the hidden title.</summary>
    [TestClass]
    public class LegendaryServerTests
    {
        [TestMethod]
        public void TheFive_AreTheDarkFortressPieces()
        {
            foreach (var l in Legendary.All)
            {
                Assert.AreEqual(GameData.DarkFortressItemId(l.Key), l.ItemId, l.Name);
                Assert.AreEqual(GameData.DarkFortressItemByName(l.Name), l.ItemId, l.Name);
            }
        }

        [TestMethod]
        public void TheStayingUniques_AreNotLegendary()
        {
            foreach (var name in new[] { "Skullcrusher", "Gravebloom Warhammer", "Sunstaff of the Inquisitor", "Dreadforged Sword" })
            {
                var id = GameData.DarkFortressItemByName(name);
                Assert.IsNotNull(id, name);
                Assert.IsFalse(Legendary.IsLegendary(id.Value), name);
            }
        }

        [TestMethod]
        public void TheTitle_IsHiddenWithTwoTiers()
        {
            Assert.AreEqual("legend", LegendaryManager.Achievement);
            Assert.AreEqual(5, Legendary.Count);
        }
    }
}
