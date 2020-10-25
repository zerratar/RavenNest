using System.Linq;
using RavenNest.BusinessLogic.Providers;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public class HighScoreManager : IHighScoreManager
    {
        private readonly IPlayerHighscoreProvider highscoreProvider;
        private readonly IPlayerManager playerManager;

        public HighScoreManager(
            IPlayerHighscoreProvider highscoreProvider, IPlayerManager playerManager)
        {
            this.highscoreProvider = highscoreProvider;
            this.playerManager = playerManager;
        }

        public HighScoreCollection GetSkillHighScore(string skill, int skip = 0, int take = 100)
        {
            var players = playerManager.GetPlayers().Where(x => !x.IsAdmin && x.CharacterIndex == 0).ToList();
            return highscoreProvider.GetSkillHighScore(players, skill, skip, take);
        }

        public HighScoreCollection GetHighScore(int skip = 0, int take = 100)
        {
            return GetSkillHighScore(null, skip, take);
        }
    }
}
