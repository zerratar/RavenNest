using System;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
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
        private readonly TelemetryClient telemetryClient;
        private readonly IMemoryCache highscoreCache;
        private readonly IHighScoreManager highScoreManager;

        public HighScoreController(
            TelemetryClient telemetryClient,
            IMemoryCache highscoreCache,
            IHighScoreManager highScoreManager)
        {
            this.telemetryClient = telemetryClient;
            this.highscoreCache = highscoreCache;
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
        public HighScoreCollection GetSkillHighScore(string skill, int offset, int skip)
        {
            return highScoreManager.GetSkillHighScore(skill, offset, skip); 
            //var key = $"highscore_{skill}_{offset}_{skip}";
            //if (highscoreCache.TryGetValue<HighScoreCollection>(key, out var highscoreData))
            //{
            //    return highscoreData;
            //}

            //telemetryClient.TrackEvent("GetSkillHighScore_SOS");
            //highscoreData = highScoreManager.GetSkillHighScore(skill, offset, skip);
            //return highscoreCache.Set(key, highscoreData, new MemoryCacheEntryOptions
            //{
            //    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
            //});
        }

        [HttpGet("paged/{offset}/{skip}")]
        [MethodDescriptor(
            Name = "Get Highscore Paging for total",
            Description = "Gets a page from the highscore using an offset and skip.",
            RequiresSession = false,
            RequiresAuth = false,
            RequiresAdmin = false)
        ]
        public HighScoreCollection GetPagedHighScore(int offset, int skip)
        {
            return highScoreManager.GetHighScore(offset, skip);
            //var key = $"highscore_{offset}_{skip}";
            //if (highscoreCache.TryGetValue<HighScoreCollection>(key, out var highscoreData))
            //{
            //    return highscoreData;
            //}

            //telemetryClient.TrackEvent("GetSkillHighScore_OS");
            //highscoreData = highScoreManager.GetHighScore(offset, skip);
            //return highscoreCache.Set(key, highscoreData, new MemoryCacheEntryOptions
            //{
            //    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
            //});
        }

        [HttpGet("{skill}")]
        [MethodDescriptor(
            Name = "Top 100 Skill",
            Description = "Gets the top 100 players in a particular skill. Ordered by level then by exp.",
            RequiresSession = false,
            RequiresAuth = false,
            RequiresAdmin = false)
        ]
        public HighScoreCollection GetSkillHighScore(string skill)
        {
            return highScoreManager.GetSkillHighScore(skill);
            //var key = $"highscore_{skill}";
            //if (highscoreCache.TryGetValue<HighScoreCollection>(key, out var highscoreData))
            //{
            //    return highscoreData;
            //}

            //telemetryClient.TrackEvent("GetSkillHighScore_S");
            //highscoreData = highScoreManager.GetSkillHighScore(skill);
            //return highscoreCache.Set(key, highscoreData, new MemoryCacheEntryOptions
            //{
            //    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
            //});
        }

        [HttpGet]
        [MethodDescriptor(
            Name = "Top 100 Players",
            Description = "Gets the top 100 players in a all/overall skill levels. Ordered by total level then by total exp.",
            RequiresSession = false,
            RequiresAuth = false,
            RequiresAdmin = false)
        ]
        public HighScoreCollection GetHighScore()
        {
            //var key = $"highscore";
            //if (highscoreCache.TryGetValue<HighScoreCollection>(key, out var highscoreData))
            //{
            //    return highscoreData;
            //}
            return highScoreManager.GetHighScore();
            //telemetryClient.TrackEvent("GetSkillHighScore");
            //highscoreData = highScoreManager.GetHighScore();
            //return highscoreCache.Set(key, highscoreData, new MemoryCacheEntryOptions
            //{
            //    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
            //});
        }

    }
}