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

        public HighScoreCollection GetSkillHighScore(string skill, int skip = 0, int take = 100, int characterIndex = 0)
        {
            var players = playerManager.GetPlayers().Where(x => !x.IsAdmin && (characterIndex == -1 || x.CharacterIndex == characterIndex)).ToList();
            return highscoreProvider.GetSkillHighScore(players, skill, skip, take);
        }

        public HighScoreCollection GetHighScore(int skip = 0, int take = 100, int characterIndex = 0)
        {
            return GetSkillHighScore(null, skip, take, characterIndex);
        }
    }
}
