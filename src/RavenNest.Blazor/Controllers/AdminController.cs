using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Net;
using RavenNest.Models;
using RavenNest.Sessions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private const string InsufficientPermissions = "You do not have permissions to call this API";
        private readonly ILogger<AdminController> logger;
        private readonly GameData gameData;
        private readonly SessionInfoProvider sessionInfoProvider;
        private readonly PlayerManager playerManager;
        private readonly AdminManager adminManager;
        private readonly IAuthManager authManager;

        public AdminController(
            ILogger<AdminController> logger,
            GameData gameData,
            SessionInfoProvider sessionInfoProvider,
            PlayerManager playerManager,
            AdminManager adminManager,
            IAuthManager authManager)
        {
            this.logger = logger;
            this.gameData = gameData;
            this.sessionInfoProvider = sessionInfoProvider;
            this.playerManager = playerManager;
            this.adminManager = adminManager;
            this.authManager = authManager;
        }

        [HttpGet("refresh-permissions")]
        public async Task<bool> RefreshPermissions()
        {
            await AssertAdminAccessAsync();
            return adminManager.RefreshPermissions();
        }

        [HttpGet("fix-index/{userId}")]
        public async Task<bool> FixIndices(string userId)
        {
            await AssertAdminAccessAsync();
            return adminManager.FixCharacterIndices(userId);
        }

        [HttpGet("fix-all-index")]
        public async Task<bool> FixIndicesForCharaters()
        {
            await AssertAdminAccessAsync();
            return adminManager.FixCharacterIndices();
        }


        [HttpGet("remove-dangling-entities")]
        public async Task<bool> RemoveDanglingEntities()
        {
            await AssertAdminAccessAsync();
            gameData.RemoveDanglingEntities();
            return true;
        }

        [HttpGet("merge-accounts")]
        public async Task<string[]> PrepareMergePlayerAccounts()
        {
            await AssertAdminAccessAsync();

            var groups = gameData.GetDuplicateUsers();
            var accs = new List<string>();
            foreach (var group in groups)
            {
                foreach (var u in group)
                {
                    accs.Add(u.Id + " - " + u.UserName);
                }
            }

            accs.Insert(0, "Use /merge-accounts/confirm to merge the following items");

            return accs.ToArray();
        }


        [HttpGet("merge-accounts/confirm")]
        public async Task<string[]> MergePlayerAccounts()
        {
            await AssertAdminAccessAsync();
            return await adminManager.MergePlayerAccounts();
        }


        [HttpGet("fix-loyalties")]
        public async Task<bool> FixLoyaltyPoints()
        {
            await AssertAdminAccessAsync();
            return adminManager.FixLoyaltyPoints();
        }

        [HttpGet("crafting-req/{itemQuery}/{requirementQuery}")]
        public async Task<bool> SetCraftingRequirement(string itemQuery, string requirementQuery)
        {
            await AssertAdminAccessAsync();
            return adminManager.SetCraftingRequirements(itemQuery, requirementQuery);
        }

        [HttpGet("debug/state-data/{playerCount}")]
        public async Task<GameSessionPlayerCache> DownloadRandomStateCache(int playerCount)
        {
            try
            {
                await AssertAdminAccessAsync();
                return adminManager.GetRandomStateCache(playerCount);
            }
            catch
            {
                return null;
            }
        }


        [HttpGet("state-data/{streamer}")]
        public async Task<GameSessionPlayerCache> DownloadStreamerStateCache(string streamer)
        {
            try
            {
                await AssertAdminAccessAsync();
                return adminManager.GetStreamerStateCache(streamer);
            }
            catch
            {
                return null;
            }
        }

        [HttpGet("ravenbot-logs/{file}")]
        public async Task<ActionResult> DownloadRavenbotLogsAsync(string file)
        {
            try
            {
                await AssertAdminAccessAsync();
            }
            catch
            {
                return Unauthorized();
            }

            if (string.IsNullOrEmpty(file))
            {
                return Content("Bad file name.");
            }

            try
            {
                var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
                var logsFolder = new DirectoryInfo(Path.Combine(currentDir.Parent.FullName, "logs"));
                var fullFileNamePath = Path.Combine(logsFolder.FullName, file);

                if (!logsFolder.Exists)
                    return NotFound();

                if (!System.IO.File.Exists(fullFileNamePath))
                    return NotFound();

                using (var inStream = new FileStream(fullFileNamePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(inStream))
                {
                    var content = await sr.ReadToEndAsync();
                    var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                    return File(bytes, "text/plain", file);
                }
            }
            catch (Exception exc)
            {
                return Content(exc.ToString());
            }
        }


        [HttpGet("backup/download")]
        [HttpGet("download/backup")]
        public async Task<ActionResult> DownloadGameState()
        {
            try
            {
                await AssertAdminAccessAsync();
            }
            catch
            {
                return NotFound();
            }

            try
            {
                return File(gameData.GetCompressedEntities(), "application/zip", "data-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".zip");
            }
            catch (Exception exc)
            {
                return Content(exc.ToString());
            }
        }

        //[HttpGet("fix-exp/{characterId}")]
        //public async Task<bool> FixExp(Guid characterId)
        //{
        //    await AssertAdminAccessAsync();
        //    return adminManager.FixCharacterExpGain(characterId);
        //}

        [HttpGet("fix-index")]
        public async Task<bool> FixIndex()
        {
            await AssertAdminAccessAsync();
            return adminManager.FixCharacterIndices(null);
        }

        [HttpGet("refresh-villages")]
        public async Task<bool> RefreshVillageInfo()
        {
            await AssertAdminAccessAsync();
            return adminManager.RefreshVillageInfo();
        }

        [HttpPost("item-recovery/{identifier}")]
        public async Task<bool> ItemRecovery(string identifier, [FromBody] string query)
        {
            try
            {
                await AssertAdminAccessAsync();
                return adminManager.ProcessItemRecovery(query, identifier);
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                throw;
            }
        }

        [HttpGet("set-password/{username}/{newPassword}")]
        public async Task<bool> SetPassword(string username, string newPassword)
        {
            try
            {
                await AssertAdminAccessAsync();
                return adminManager.SetPassword(username, newPassword);
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                throw;
            }
        }


        [HttpGet("set-skill/{character}/{identifier}/{skill}/{level}/{experience}")]
        public async Task<bool> SetSkillLevel(string character, string identifier, string skill, int level, double experience)
        {
            try
            {
                await AssertAdminAccessAsync();
                return adminManager.SetSkillLevel(character, identifier, skill, level, experience);
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                throw;
            }
        }

        [HttpGet("set-skill/{character}/{identifier}/{skill}/{level}")]
        public async Task<bool> SetSkillLevel(string character, string identifier, string skill, int level)
        {
            try
            {
                await AssertAdminAccessAsync();
                return adminManager.SetSkillLevel(character, identifier, skill, level);
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                throw;
            }
        }

        [HttpGet("item-recovery/{identifier}/{query}")]
        public async Task<bool> ItemRecoveryAsync(string identifier, string query)
        {
            try
            {
                await AssertAdminAccessAsync();
                return adminManager.ProcessItemRecovery(query, identifier);
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                throw;
            }
        }


        [HttpGet("add-coins/{identifier}/{query}")]
        public async Task<bool> AddCoinsAsync(string identifier, string query)
        {
            try
            {
                await AssertAdminAccessAsync();
                return adminManager.AddCoins(query, identifier);
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                throw;
            }
        }

        [HttpGet("subs/{from}/{to}/{amount}")]
        public async Task<bool> LoyaltySubs(string from, string to, int amount)
        {
            try
            {
                await AssertAdminAccessAsync();
                return playerManager.LoyaltyGift(from, to, 0, amount);
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                throw;
            }
        }

        [HttpGet("cheer/{from}/{to}/{amount}")]
        public async Task<bool> LoyaltyCheerBits(string from, string to, int amount)
        {
            try
            {
                await AssertAdminAccessAsync();
                return playerManager.LoyaltyGift(from, to, amount, 0);
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                throw;
            }
        }

        [HttpGet("nerf-items")]
        public async Task<bool> NerfItemStacks()
        {
            await AssertAdminAccessAsync();
            return adminManager.NerfItems();
        }


        [HttpGet("players/{offset}/{size}/{order}/{query}")]
        public async Task<PagedPlayerCollection> GetPlayers(int offset, int size, string order, string query)
        {
            await AssertAdminAccessAsync();
            return adminManager.GetPlayersPaged(offset, size, order, query);
        }

        [HttpGet("sessions/{offset}/{size}/{order}/{query}")]
        public async Task<PagedSessionCollection> GetSessions(int offset, int size, string order, string query)
        {
            await AssertAdminAccessAsync();
            return adminManager.GetSessionsPaged(offset, size, order, query);
        }

        [HttpGet("mergeplayer/{userid}")]
        public async Task<bool> MergePlayerAccounts(Guid userid)
        {
            await AssertAdminAccessAsync();
            return adminManager.MergePlayerAccounts(userid);
        }

        [HttpGet("resetpassword/{userid}")]
        public async Task<bool> ResetUserPassword(string userid)
        {
            await AssertAdminAccessAsync();
            return adminManager.ResetUserPassword(userid);
        }

        [HttpGet("updateplayername/{characterId}/{name}")]
        public async Task<bool> UpdatePlayerName(Guid characterId, string identifier, string name)
        {
            await AssertAdminAccessAsync();
            return adminManager.UpdatePlayerName(characterId, name);
        }

        [HttpGet("updateplayerskill/{characterId}/{skill}/{experience}")]
        public async Task<bool> UpdatePlayerSkill(Guid characterId, string skill, double experience)
        {
            await AssertAdminAccessAsync();
            return adminManager.UpdatePlayerSkill(characterId, skill, experience);
        }

        [HttpGet("kick/{characterId}")]
        public async Task<bool> KickPlayer(Guid characterId)
        {
            await AssertAdminAccessAsync();
            return adminManager.KickPlayer(characterId);
        }

        [HttpGet("suspend/{userid}")]
        public async Task<bool> SuspendPlayer(string userid)
        {
            await AssertAdminAccessAsync();
            return adminManager.SuspendPlayer(userid);
        }
        private async Task AssertAdminAccessAsync()
        {
            var authToken = GetAuthToken();
            if (authToken != null)
            {
                AssertAdminAuthToken(authToken);
                return;
            }

            var twitchUser = await sessionInfoProvider.GetTwitchUserAsync(SessionId);
            AssertAdminTwitchUser(twitchUser);
        }

        private string SessionId => SessionCookie.GetSessionId(HttpContext);

        private AuthToken GetAuthToken()
        {
            if (HttpContext.Request.Headers.TryGetValue("auth-token", out var value))
                return authManager.Get(value);
            if (sessionInfoProvider.TryGetAuthToken(SessionId, out var authToken))
                return authToken;
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertAdminAuthToken(AuthToken authToken)
        {
            if (authToken == null) throw new Exception(InsufficientPermissions);
            var user = gameData.GetUser(authToken.UserId);
            if (!user.IsAdmin.GetValueOrDefault()) throw new Exception(InsufficientPermissions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertAdminTwitchUser(Twitch.TwitchRequests.TwitchUser twitchUser)
        {
            if (twitchUser == null) throw new Exception(InsufficientPermissions);
            var user = gameData.GetUserByTwitchId(twitchUser.Id);
            if (!user.IsAdmin.GetValueOrDefault()) throw new Exception(InsufficientPermissions);
        }
    }
}
