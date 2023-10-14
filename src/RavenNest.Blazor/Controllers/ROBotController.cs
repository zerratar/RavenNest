using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.Blazor.Discord.Models;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
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



        [HttpGet("discord/account/{username}")]
        public async Task<AcccountInfo> GetAccountInfoAsync(string username)
        {
            try
            {
                var user = gameData.GetUserByUsername(username);
                if (user == null)
                {
                    return new AcccountInfo(0, 0, 0, "No such user account exists.");
                }

                var res = gameData.GetResources(user.Resources.Value);
                var coins = (long)res.Coins;

                var halloweenTokens = 0L;
                var christmasTokens = 0L;
                var knownItems = gameData.GetKnownItems();
                foreach (var i in gameData.GetUserBankItems(user.Id))
                {
                    if (i.ItemId == knownItems.HalloweenToken.Id)
                    {
                        halloweenTokens += i.Amount;
                    }

                    if (i.ItemId == knownItems.ChristmasToken.Id)
                    {
                        christmasTokens += i.Amount;
                    }
                }

                return new AcccountInfo(coins, halloweenTokens, christmasTokens, null);
            }
            catch (Exception exc)
            {
                return new AcccountInfo(0, 0, 0, exc.ToString());
            }
        }

        [HttpGet("discord/account-worth/{username}")]
        public async Task<long> GetTotalAccountWorthAsync(string username)
        {
            try
            {
                var user = gameData.GetUserByUsername(username);
                if (user == null)
                {
                    return -1;
                }

                long totalValue = 0;
                var chars = gameData.GetCharactersByUserId(user.Id);
                foreach (var c in chars)
                {
                    foreach (var inv in gameData.GetInventoryItems(c.Id))
                    {
                        var item = gameData.GetItem(inv.ItemId);
                        totalValue += item.ShopSellPrice * inv.Amount ?? 0;
                    }
                }

                foreach (var i in gameData.GetUserBankItems(user.Id))
                {
                    var item = gameData.GetItem(i.ItemId);
                    totalValue += item.ShopSellPrice * i.Amount;
                }

                return totalValue;
            }
            catch (Exception exc)
            {
                return -1;
            }
        }


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

                var cs = ModelMapper.Map(state);
                var skills = gameData.GetCharacterSkills(character.SkillsId);
                var combatLevel = GameData.GetCombatLevel(skills);
                var stats = GetStats(skills.GetSkills());
                result[i] = new CharacterInfo(
                    character.Id, character.CharacterIndex, character.Name, character.Identifier, combatLevel,
                    stream, training, cs.Island, cs.RestedTime, cs.InDungeon, cs.InRaid,
                    cs.InOnsen, cs.Destination, cs.EstimatedTimeForLevelUp, cs.ExpPerHour, stats);
            }
            return result;
        }

        private Stats[] GetStats(IReadOnlyList<StatsUpdater> skills)
        {
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
