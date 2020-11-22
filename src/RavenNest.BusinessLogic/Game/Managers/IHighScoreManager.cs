using System.Threading.Tasks;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IHighScoreManager
    {
        HighScoreCollection GetSkillHighScore(string skill, int skip = 0, int take = 100);
        HighScoreCollection GetHighScore(int skip = 0, int take = 100);
    }
}
