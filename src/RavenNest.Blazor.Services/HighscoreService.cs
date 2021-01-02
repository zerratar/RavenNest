using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class HighscoreService
    {
        private readonly IHighScoreManager highScoreManager;

        public HighscoreService(IHighScoreManager highScoreManager)
        {
            this.highScoreManager = highScoreManager;
        }

        public HighScoreCollection GetHighscore(int offset, int take, int characterIndex)
        {
            return highScoreManager.GetHighScore(offset, take, characterIndex);
        }

        public HighScoreCollection GetHighscore(string skill, int offset, int take, int characterIndex)
        {
            return highScoreManager.GetSkillHighScore(skill, offset, take, characterIndex);
        }

        public async Task<HighScoreCollection> GetHighscoreAsync(int offset, int take, int characterIndex)
        {
            return await Task.Run(() => GetHighscore(offset, take, characterIndex));
        }

        public async Task<HighScoreCollection> GetHighscoreAsync(string skill, int offset, int take, int characterIndex)
        {
            return await Task.Run(() => GetHighscore(skill, offset, take, characterIndex));
        }
    }
}
