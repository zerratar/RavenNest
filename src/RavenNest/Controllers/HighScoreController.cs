using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RavenNest.BusinessLogic.Docs.Attributes;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ApiDescriptor(Name = "HighScore API", Description = "Used for retrieving player HighScore list.")]
    public class HighScoreController : ControllerBase
    {
        private readonly IHighScoreManager highScoreManager;

        public HighScoreController(IHighScoreManager highScoreManager)
        {
            this.highScoreManager = highScoreManager;
        }

        [HttpGet("paged/{skill}/{offset}/{skip}")]
        [MethodDescriptor(
            Name = "Get Highscore Paging for skill",
            Description = "Gets a page from the highscore using an offset and skip.",
            RequiresSession = false,
            RequiresAuth = false,
            RequiresAdmin = false)
        ]
        public Task<HighScoreCollection> GetSkillHighScore(string skill, int offset, int skip)
        {
            return highScoreManager.GetSkillHighScore(skill, offset, skip);
        }

        [HttpGet("paged/{offset}/{skip}")]
        [MethodDescriptor(
            Name = "Get Highscore Paging for total",
            Description = "Gets a page from the highscore using an offset and skip.",
            RequiresSession = false,
            RequiresAuth = false,
            RequiresAdmin = false)
        ]
        public Task<HighScoreCollection> GetPagedHighScore(int offset, int skip)
        {
            return highScoreManager.GetHighScore(offset, skip);
        }

        [HttpGet("{skill}")]
        [MethodDescriptor(
            Name = "Top 100 Skill",
            Description = "Gets the top 100 players in a particular skill. Ordered by level then by exp.",
            RequiresSession = false,
            RequiresAuth = false,
            RequiresAdmin = false)
        ]
        public Task<HighScoreCollection> GetSkillHighScore(string skill)
        {
            return highScoreManager.GetSkillHighScore(skill);
        }

        [HttpGet]
        [MethodDescriptor(
            Name = "Top 100 Players",
            Description = "Gets the top 100 players in a all/overall skill levels. Ordered by total level then by total exp.",
            RequiresSession = false,
            RequiresAuth = false,
            RequiresAdmin = false)
        ]
        public Task<HighScoreCollection> GetHighScore()
        {
            return highScoreManager.GetHighScore();
        }

    }
}