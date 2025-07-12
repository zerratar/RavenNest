using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.BusinessLogic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RavenNest.UnitTests
{
    [TestClass]
    public class GameStateParsingUnitTests
    {
        [TestMethod]
        public void ParseRawGameState_ReturnsValidGameStateObject()
        {
            var stateFile = @"C:\Ravenfall\Data\user-states\20250706_080858_zerratar.state";
            var stateData = GameStateParser.Parse(stateFile);
            Assert.IsNotNull(stateData, "Parsed game state should not be null.");
            var gs = stateData.GameState;
            var xp = stateData.ExperienceState;
            var ps = stateData.PlayerState;

            Assert.IsNotNull(gs, "Game state should not be null.");
            Assert.IsNotNull(xp, "Experience state should not be null.");
            Assert.IsNotNull(ps, "Player state should not be null.");

        }
    }
}
