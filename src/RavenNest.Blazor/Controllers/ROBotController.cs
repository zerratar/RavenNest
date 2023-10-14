using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.Blazor.Discord.Models;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Game.Enchantment;
using RavenNest.DataModels;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

                var c = ModelMapper.Map(character, gameData);
                var cs = c.State;
                var skills = gameData.GetCharacterSkills(character.SkillsId);
                var combatLevel = GameData.GetCombatLevel(skills);
                var stats = GetStats(skills.GetSkills());
                result[i] = new CharacterInfo(
                    character.Id, character.CharacterIndex, character.Name, character.Identifier, combatLevel,
                    stream, training, cs.Island, cs.RestedTime, cs.InDungeon, cs.InRaid,
                    cs.InOnsen, cs.Destination, cs.EstimatedTimeForLevelUp, cs.ExpPerHour, stats, GetEquipment(c));
            }
            return result;
        }

        private CharacterEquipment GetEquipment(Player c)
        {
            double totalArmorPower = 0;
            double totalWeaponAim = 0;
            double totalWeaponPower = 0;
            double totalRangedAim = 0;
            double totalRangedPower = 0;
            double totalMagicAim = 0;
            double totalMagicPower = 0;

            var eqNames = new List<string>();
            foreach (var eq in c.InventoryItems.Where(x => x.Equipped))
            {
                eqNames.Add(eq.Name);
                var i = gameData.GetItem(eq.ItemId);
                totalArmorPower += i.ArmorPower;
                totalWeaponAim += i.WeaponAim;
                totalWeaponPower += i.WeaponPower;
                totalRangedAim += i.RangedAim;
                totalRangedPower += i.RangedPower;
                totalMagicAim += i.MagicAim;
                totalMagicPower += i.MagicPower;

                var enchantments = eq.GetItemEnchantments(gameData);
                foreach (var e in enchantments)
                {
                    if (e.Name.ToLower().Contains("power"))
                    {
                        totalWeaponPower += (i.WeaponPower * e.Value);
                        totalRangedPower += (i.RangedPower * e.Value);
                        totalMagicPower += (i.MagicPower + e.Value);
                    }

                    if (e.Name.ToLower().Contains("aim"))
                    {
                        totalWeaponAim += (i.WeaponAim * e.Value);
                        totalRangedAim += (i.RangedAim * e.Value);
                        totalMagicAim += (i.MagicAim + e.Value);
                    }


                    if (e.Name.ToLower().Contains("armor") || e.Name.ToLower().Contains("armour"))
                    {
                        totalArmorPower += (i.ArmorPower * e.Value);
                    }
                }
            }

            return new CharacterEquipment(totalArmorPower, totalWeaponAim, totalWeaponPower, totalRangedAim, totalRangedPower, totalMagicAim, totalMagicPower, eqNames.ToArray());
        }

        private Stats[] GetStats(IReadOnlyList<StatsUpdater> skills)
        {
            Stats[] result = new Stats[skills.Count];
            for (var i = 0; i < skills.Count; ++i)
            {
                var skill = skills[i];
                var exp = skill.Experience;
                var nextLevel = GameMath.ExperienceForLevel(skill.Level + 1);
                var progress = exp / nextLevel;
                result[i] = new Stats(skill.Name, skill.Level, (long)exp, (long)nextLevel, (float)progress);
            }
            return result;
        }

        #endregion
    }
}
