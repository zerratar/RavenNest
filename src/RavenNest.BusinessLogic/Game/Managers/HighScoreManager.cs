using System.Linq;
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
            var players = playerManager.GetHighscorePlayers(characterIndex);

            if (string.IsNullOrEmpty(skill) || skill.ToLower() == "all")
            {
                return highscoreProvider.GetHighScoreAllSkills(players, skip, take);
            }

            return highscoreProvider.GetSkillHighScore(players, skill, skip, take);
        }

        public HighScoreCollection GetHighScore(int skip = 0, int take = 100, int characterIndex = 0)
        {
            return GetSkillHighScore(null, skip, take, characterIndex);
        }
    }
}
