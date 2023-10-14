using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.Blazor.Discord.Models;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.DataModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace RavenNest.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("api/[controller]")]
    [ApiController]
    public class ROBotController : ControllerBase
    {
        private readonly ILogger<ROBotController> logger;
        private readonly GameData gameData;
        private readonly IServerManager serverManager;

        public ROBotController(
            ILogger<ROBotController> logger,
            GameData gameData,
            IServerManager serverManager)
        {
            this.logger = logger;
            this.gameData = gameData;
            this.serverManager = serverManager;
        }

        [HttpPost("stats")]
        public async Task OnStatsPosted()
        {
            var contentLength = HttpContext.Request.ContentLength;
            if (contentLength > 0)
            {
                var sr = new StreamReader(HttpContext.Request.Body);
                var data = await sr.ReadToEndAsync();
                serverManager.UpdateBotStats(data);
            }
        }


        #region Discord Bot Apis

        //[HttpGet("discord/resources/{username}")]
        //public async Task<CharacterList> GetCharacterListByUserNameAsync(string username)
        //{
        //    try
        //    {
        //        var user = gameData.GetUserByUsername(username);
        //        if (user == null)
        //        {
        //            return new CharacterList(null, "No account with the username '" + username + "' exists.");
        //        }

        //        var chars = GetCharacterInfos(gameData.GetCharactersByUserId(user.Id));
        //        return new CharacterList(chars, null);
        //    }
        //    catch (Exception exc)
        //    {
        //        return new CharacterList(null, exc.ToString());
        //    }
        //}



        [HttpGet("discord/character-list/{username}")]
        public async Task<CharacterList> GetCharacterListByUserNameAsync(string username)
        {
            try
            {
                var user = gameData.GetUserByUsername(username);
                if (user == null)
                {
                    return new CharacterList(null, "No account with the username '" + username + "' exists.");
                }

                var chars = GetCharacterInfos(gameData.GetCharactersByUserId(user.Id));
                return new CharacterList(chars, null);
            }
            catch (Exception exc)
            {
                return new CharacterList(null, exc.ToString());
            }
        }

        private CharacterInfo[] GetCharacterInfos(IReadOnlyList<Character> characters)
        {
            CharacterInfo[] result = new CharacterInfo[characters.Count];
            for (var i = 0; i < characters.Count; ++i)
            {
                var character = characters[i];
                var stream = character.UserIdLock != null ? gameData.GetUser(character.UserIdLock.Value).UserName : null;
                var state = gameData.GetCharacterState(character.StateId);

                var training = state.Task;
                if (state.Task == "Fighting")
                {
                    training = state.TaskArgument;
                }

                var stats = GetStats(character);
                result[i] = new CharacterInfo(character.Id, character.CharacterIndex, character.Name, character.Identifier, stream, training, stats);
            }
            return result;
        }

        private Stats[] GetStats(Character character)
        {
            var skills = gameData.GetCharacterSkills(character.SkillsId).GetSkills();
            Stats[] result = new Stats[skills.Count];
            for (var i = 0; i < skills.Count; ++i)
            {
                var skill = skills[i];
                result[i] = new Stats(skill.Name, skill.Level);
            }
            return result;
        }

        #endregion
    }
}
