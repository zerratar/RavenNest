using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HighScoreController : ControllerBase
    {
        private readonly HighScoreManager highScoreManager;

        public HighScoreController(HighScoreManager highScoreManager)
        {
            this.highScoreManager = highScoreManager;
        }

        [HttpGet("paged/{skill}/{offset}/{skip}")]
        public HighScoreCollection GetSkillHighScore(string skill, int offset, int skip)
        {
            return highScoreManager.GetSkillHighScore(skill, offset, skip);
        }

        [HttpGet("paged/{offset}/{skip}")]
        public HighScoreCollection GetPagedHighScore(int offset, int skip)
        {
            return highScoreManager.GetHighScore(offset, skip);
        }

        [HttpGet("{skill}")]
        public HighScoreCollection GetSkillHighScore(string skill)
        {
            return highScoreManager.GetSkillHighScore(skill);
        }

        [HttpGet]
        public HighScoreCollection GetHighScore()
        {
            return highScoreManager.GetHighScore();
        }
    }
}
