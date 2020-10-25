using RavenNest.Models;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Game
{
    public interface IPlayerHighscoreProvider
    {
        HighScoreCollection GetSkillHighScore(IReadOnlyList<Player> players, string skill, int skip = 0, int take = 100);
        HighScoreCollection GetHighScore(IReadOnlyList<Player> players, int skip = 0, int take = 100);
        HighScoreItem GetSkillHighScore(Player player, IReadOnlyList<Player> players, string skill);
    }
}
