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

        public HighScoreCollection GetHighscore(int offset, int take)
        {
            return highScoreManager.GetHighScore(offset, take);
        }

        public async Task<HighScoreCollection> GetHighscoreAsync(int offset, int take)
        {
            return await Task.Run(() => GetHighscore(offset, take));
        }
    }
}
