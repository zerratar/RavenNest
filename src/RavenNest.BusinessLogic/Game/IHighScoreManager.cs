using System.Threading.Tasks;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IHighScoreManager
    {
        Task<HighScoreCollection> GetSkillHighScore(string skill, int skip = 0, int take = 100);
        Task<HighScoreCollection> GetHighScore(int skip = 0, int take = 100);
    }
}