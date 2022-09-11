using RavenNest.BusinessLogic.Models;
using RavenNest.Models;
using System;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Game
{
    public interface IPlayerHighscoreProvider
    {
        HighScoreCollection GetSkillHighScore(IReadOnlyDictionary<Guid, HighscorePlayer> playerLookup, string skill, int skip = 0, int take = 100);
        HighScoreCollection GetHighScoreAllSkills(IReadOnlyDictionary<Guid, HighscorePlayer> players, int skip, int take);
        HighScoreItem GetSkillHighScore(Player player, IReadOnlyDictionary<Guid, HighscorePlayer> players, string skill);
    }
}
