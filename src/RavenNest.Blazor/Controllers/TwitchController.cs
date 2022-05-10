using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RavenNest.Blazor.Services;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Docs.Attributes;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;
using RavenNest.Twitch;
using static RavenNest.Twitch.TwitchRequests;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ApiDescriptor(Name = "Twitch API", Description = "Used by the website to allow authentication with Twitch. This is not meant to be used elsewhere.")]
    public class TwitchController : ControllerBase
    {
        private readonly IPlayerManager playerManager;
        private readonly IGameData gameData;
        private readonly ISessionInfoProvider sessionInfoProvider;
        private readonly IAuthManager authManager;
        private readonly LogoService logoService;
        private readonly AppSettings settings;

        private readonly byte[] unknownProfilePictureBytes;
        private readonly byte[] unknownClanLogoBytes;
        private readonly string unknownProfilePictureUrl;
        private readonly string unknownClanLogoUrl;

        public TwitchController(
            IOptions<AppSettings> settings,
            IPlayerManager playerManager,
            IGameData gameData,
            ISessionInfoProvider sessionInfoProvider,
            IAuthManager authManager,
            LogoService logoService)
        {
            this.playerManager = playerManager;
            this.gameData = gameData;
            this.sessionInfoProvider = sessionInfoProvider;
            this.authManager = authManager;
            this.logoService = logoService;
            this.settings = settings.Value;

            var a = unknownProfilePictureUrl = "imgs/ravenfall_logo_tiny.png";
            var b = unknownClanLogoUrl = "imgs/logo-tiny-black.png";

            if (!System.IO.File.Exists(a))
            {
                a = Path.Combine("wwwroot", a);
                b = Path.Combine("wwwroot", b);
            }

            if (System.IO.File.Exists(a))
            {
                this.unknownProfilePictureBytes = System.IO.File.ReadAllBytes(a);
            }

            if (System.IO.File.Exists(b))
            {
                this.unknownClanLogoBytes = System.IO.File.ReadAllBytes(b);
            }
        }
        [ApiExplorerSettings(IgnoreApi = true)]

        [HttpGet("authorize")]
        public async Task<ActionResult> OAuthAuthorize()
        {
            var reqCode = HttpContext.Request.Query["code"];
            var reqState = HttpContext.Request.Query["state"];
#if DEBUG
            var requestUrl = $"https://{HttpContext.Request.Host}/login/twitch";
            Console.WriteLine(requestUrl);
#else 
            var requestUrl = "https://www.ravenfall.stream/login/twitch";
#endif
            try
            {
                var sessionInfo = await TwitchAuthenticateAsync(reqCode);
                if (sessionInfo != null)
                {
                    requestUrl += "?token=" + sessionInfo.access_token + "&state=" + reqState;

                    var req = new TwitchRequests(sessionInfo.access_token, settings.TwitchClientId, settings.TwitchClientSecret);
                    var info = await req.ValidateOAuthTokenAsync();
                    if (info != null)
                    {
                        requestUrl += "&id=" + info.ClientID + "&user=" + info.Login;
                    }
                }
            }
            catch
            {
                // ignored
            }

            return Redirect(requestUrl);
        }

        [HttpGet("logo/{userId}")]

        [ResponseCache(VaryByHeader = "User-Agent", Duration = 600)]
        public async Task<ActionResult> GetChannelPictureAsync(string userId)
        {
            try
            {
                var imageData = await logoService.GetChannelPictureAsync(userId);
                if (imageData != null)
                {
                    return File(imageData, "image/png");
                }

                if (unknownProfilePictureBytes == null)
                {
                    return NotFound();
                }

                //return Redirect(unknownProfilePictureUrl);
                return File(unknownProfilePictureBytes, "image/png");
            }
            catch { }
            return NotFound();
        }

        [HttpGet("clan-logo/{userId}")]
        [ResponseCache(VaryByHeader = "User-Agent", Duration = 600)]
        public async Task<ActionResult> GetClanLogoAsync(string userId)
        {
            try
            {
                var imageData = await logoService.GetClanLogoAsync(userId);
                if (imageData != null)
                {
                    return File(imageData, "image/png");
                }

                if (unknownClanLogoBytes == null)
                {
                    return NotFound();
                }

                //return Redirect(unknownClanLogoUrl);
                return File(unknownClanLogoBytes, "image/png");
            }
            catch { }
            return NotFound();
        }
        [ApiExplorerSettings(IgnoreApi = true)]

        [HttpGet("clear-logo/{userId}")]
        public bool ClearClanLogoAsync(string userId)
        {
            try
            {
                if (!TryGetSession(out var sessionInfo))
                {
                    return false;
                }

                if (sessionInfo.UserId != userId)
                {
                    return false;
                }

                return logoService.ClearLogos(userId);
            }
            catch { }
            return false;
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("session/{token}")]
        [MethodDescriptor(Name = "Set Twitch Access Token", Description = "Updates current session with the set Twitch access token, used as an user identifier throughout the website.")]
        public async Task<SessionInfo> SetAccessToken(string token)
        {
            var session = this.HttpContext.GetSessionId();
            var result = await sessionInfoProvider.SetTwitchTokenAsync(session, token);
            var user = await sessionInfoProvider.GetTwitchUserAsync(session, token);
            if (user != null)
            {
                playerManager.CreatePlayerIfNotExists(user.Id, user.Login, "1");
            }
            return result.SessionInfo;
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("extension/set-task/{broadcasterId}/{characterId}/{task}")]
        public bool SetTask(string broadcasterId, Guid characterId, string task)
        {
            return SetTask(broadcasterId, characterId, task, null);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("extension/set-task/{broadcasterId}/{characterId}/{task}/{taskArgument}")]
        public bool SetTask(string broadcasterId, Guid characterId, string task, string taskArgument)
        {
            if (string.IsNullOrEmpty(broadcasterId))
            {
                return false;
            }

            if (!TryGetSession(out var sessionInfo))
            {
                return false;
            }

            var activeSession = gameData.GetOwnedSessionByUserId(broadcasterId);
            if (activeSession == null)
            {
                return false;
            }

            var user = gameData.GetUser(sessionInfo.AccountId);
            if (user == null)
            {
                return false;
            }

            var myCharacters = gameData.GetCharacters(c => c.UserId == user.Id && c.Id == characterId);
            var character = myCharacters.FirstOrDefault();
            if (character == null)
            {
                return false;
            }

            playerManager.SendPlayerTaskToGame(activeSession, character, task, taskArgument);
            return true;
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("extension/player/{broadcasterId}")]
        public WebsitePlayer GetActivePlayer(string broadcasterId)
        {
            var activeSession = gameData.GetOwnedSessionByUserId(broadcasterId);
            if (activeSession == null)
            {
                return null;
            }

            if (TryGetSession(out var si))
            {
                var activeCharacter = gameData.GetCharacterBySession(activeSession.Id, si.UserId);
                var user = gameData.GetUser(si.AccountId);
                return activeCharacter == null || user == null
                    ? null : playerManager.GetWebsitePlayer(user, activeCharacter);
            }
            return null;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("extension/leave/{broadcasterId}/{characterId}")]
        public bool PlayerLeave(string broadcasterId, Guid characterId)
        {
            if (string.IsNullOrEmpty(broadcasterId))
            {
                return false;
            }

            if (!TryGetSession(out var sessionInfo))
            {
                return false;
            }

            var activeSession = gameData.GetOwnedSessionByUserId(broadcasterId);
            if (activeSession == null)
            {
                return false;
            }

            var user = gameData.GetUser(sessionInfo.AccountId);
            if (user == null)
            {
                return false;
            }

            var myCharacters = gameData.GetCharacters(c => c.UserId == user.Id && c.Id == characterId);
            var character = myCharacters.FirstOrDefault();
            if (character == null)
            {
                return false;
            }

            if (playerManager.SendRemovePlayerFromSessionToGame(character))
            {
                sessionInfoProvider.SetActiveCharacter(sessionInfo, null);
                character.UserIdLock = null;
                return true;
            }
            // playerManager.RemovePlayerFromActiveSession(activeSession, characterId)
            return false;
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("extension/create-join/{broadcasterId}")]
        public async Task<ExtensionPlayerJoinResult> PlayerJoin(string broadcasterId)
        {
            var result = new ExtensionPlayerJoinResult();
            if (string.IsNullOrEmpty(broadcasterId))
            {
                result.ErrorMessage = "Invalid broadcaster or bad character id";
                return result;
            }

            var activeSession = gameData.GetOwnedSessionByUserId(broadcasterId);
            if (activeSession == null)
            {
                result.ErrorMessage = "Broadcaster does not have an active game session";
                return result;
            }

            if (TryGetSession(out var sessionInfo))
            {
                var user = gameData.GetUser(sessionInfo.AccountId);
                if (user == null)
                {
                    result.ErrorMessage = "No such user.";
                    return result;
                }

                var existingCharacters = gameData.GetCharactersByUserId(user.Id);
                if (existingCharacters.Count >= 3)
                {
                    result.ErrorMessage = "You already have 3 characters.";
                    return result;
                }

                var index = existingCharacters.Count + 1;
                var player = await playerManager.CreatePlayer(user.UserId, user.UserName, index.ToString());
                if (player == null)
                {
                    result.ErrorMessage = "Failed to create a new character.";
                    return result;
                }

                var c = gameData.GetCharacter(player.Id);
                if (c == null)
                {
                    result.ErrorMessage = "Okay. We don't know what went wrong here. Very sorry!";
                    return result;
                }

                // We are just going to assume that it was successeful
                // since, if it "fails" as the character is already in game. well. Then what harm done?
                result.Success = true;
                result.Player = c.MapForWebsite(gameData, user);

                sessionInfoProvider.SetActiveCharacter(sessionInfo, c.Id);

                var gameEvent = gameData.CreateSessionEvent(GameEventType.PlayerAdd,
                    activeSession,
                    new PlayerAdd()
                    {
                        CharacterId = c.Id,
                        Identifier = c.Identifier,
                        UserId = sessionInfo.UserId,
                        UserName = sessionInfo.UserName
                    });

                gameData.Add(gameEvent);
            }
            else
            {
                result.ErrorMessage = "You don't seem to be authenticated.";
                return result;
            }

            return result;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("extension/join/{broadcasterId}/{characterId}")]
        public ExtensionPlayerJoinResult PlayerJoin(string broadcasterId, Guid characterId)
        {
            var result = new ExtensionPlayerJoinResult();
            if (string.IsNullOrEmpty(broadcasterId) || characterId == Guid.Empty)
            {
                result.ErrorMessage = "Invalid broadcaster or bad character id";
                return result;
            }

            var activeSession = gameData.GetOwnedSessionByUserId(broadcasterId);
            if (activeSession == null)
            {
                result.ErrorMessage = "Broadcaster does not have an active game session";
                return result;
            }

            if (TryGetSession(out var sessionInfo))
            {
                var c = gameData.GetCharacter(characterId);
                if (c == null)
                {
                    result.ErrorMessage = "No such character.";
                    return result;
                }

                var myUser = gameData.GetUser(c.UserId);
                if (myUser == null || myUser.UserId != sessionInfo.UserId)
                {
                    result.ErrorMessage = "No such user.";
                    return result;
                }

                if (c.UserId != myUser.Id)
                {
                    result.ErrorMessage = "Well, that is not your character.";
                    return result;
                }

                // We are just going to assume that it was successeful
                // since, if it "fails" as the character is already in game. well. Then what harm done?
                result.Success = true;
                result.Player = c.MapForWebsite(gameData, myUser);


                sessionInfoProvider.SetActiveCharacter(sessionInfo, characterId);

                var gameEvent = gameData.CreateSessionEvent(GameEventType.PlayerAdd,
                    activeSession,
                    new PlayerAdd()
                    {
                        CharacterId = characterId,
                        Identifier = c.Identifier,
                        UserId = sessionInfo.UserId,
                        UserName = sessionInfo.UserName
                    });

                gameData.Add(gameEvent);
            }
            else
            {
                result.ErrorMessage = "You don't seem to be authenticated.";
                return result;
            }

            return result;
        }
        [ApiExplorerSettings(IgnoreApi = true)]

        [HttpGet("extension/new/{broadcasterId}/{userId}/{username}/{displayName}")]
        public Task<SessionInfo> CreateUserAsync(string broadcasterId, string userId, string username, string displayName)
        {
            if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(username))
            {
                if (userId.StartsWith("u")) userId = userId.Substring(1);
                playerManager.CreatePlayerIfNotExists(userId, username, "1");
            }
            return SetExtensionViewer(broadcasterId, userId);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("extension/{broadcasterId}")]
        public StreamerInfo GetStreamerInfo(string broadcasterId)
        {
            var streamer = gameData.GetUserByTwitchId(broadcasterId);
            var result = new StreamerInfo();
            if (streamer != null)
            {
                result.StreamerUserId = broadcasterId;
                result.StreamerUserName = streamer.UserName;

                var gameSession = gameData.GetOwnedSessionByUserId(streamer.UserId);
                result.IsRavenfallRunning = gameSession != null;
                result.StreamerSessionId = gameSession?.Id;


                if (gameSession != null)
                {
                    result.Started = gameSession.Started;

                    var state = gameData.GetSessionState(gameSession.Id);
                    if (state != null)
                    {
                        result.ClientVersion = state.ClientVersion;
                    }

                    var charactersInSession = gameData.GetSessionCharacters(gameSession);
                    if (charactersInSession != null)
                    {
                        var session = this.HttpContext.GetSessionId();
                        if (sessionInfoProvider.TryGet(session, out var sessionInfo))
                        {
                            var u = gameData.GetUser(sessionInfo.AccountId);
                            if (u != null)
                            {
                                var c = charactersInSession.FirstOrDefault(x => x.UserId == u.Id);
                                if (c != null)
                                {
                                    result.JoinedCharacterId = c.Id;
                                }
                            }
                        }
                        result.PlayerCount = charactersInSession.Count;
                    }
                }
            }

            return result;
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("extension/{broadcasterId}/{viewerId}")]
        public async Task<SessionInfo> SetExtensionViewer(string broadcasterId, string viewerId)
        {
            var session = this.HttpContext.GetSessionId();

            // We need to clear out previous sessions of the combinations of broadcasterId and viewerId.
            // we will use the pair to retake existing one
            var result = await sessionInfoProvider.CreateTwitchUserSessionAsync(session, broadcasterId, viewerId);
            return result;
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("access")]
        [MethodDescriptor(Name = "Get Access Token Request URL", Description = "Gets a Twitch access token request url with the scope user:read:email.")]
        public string GetAccessTokenRequestUrl()
        {
            return $"https://id.twitch.tv/oauth2/authorize?client_id={settings.TwitchClientId}&redirect_uri="
            + $"https://{HttpContext.Request.Host}/login/twitch"
            + "&response_type=token&scope=user:read:email";

            //    if (!string.IsNullOrEmpty(settings.DevelopmentServer))
            //    {
            //        return $"https://id.twitch.tv/oauth2/authorize?client_id={settings.TwitchClientId}&redirect_uri="
            //        + $"https://{HttpContext.Request.Host}/login/twitch"
            //        + "&response_type=token&scope=user:read:email";
            //    }
            //    else
            //    {
            //        return $"https://id.twitch.tv/oauth2/authorize?client_id={settings.TwitchClientId}&redirect_uri="
            //        + "https://www.ravenfall.stream/login/twitch"
            //        + "&response_type=token&scope=user:read:email";
            //    }
            //}
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("user")]
        [MethodDescriptor(Name = "Get Twitch User", Description = "After authenticating with Twitch, this can be used to get information about the logged in user.")]
        public async Task<string> GetTwitchUser()
        {
            if (sessionInfoProvider.TryGet(HttpContext.GetSessionId(), out var session))
            {
                return $"{{ \"login\": \"{session.UserName}\", \"id\": \"{session.UserId}\"}}";
            }

            if (!this.sessionInfoProvider.TryGetTwitchToken(HttpContext.GetSessionId(), out var key))
            {
                return "nope";
            }

            var twitch = new TwitchRequests(key, settings.TwitchClientId, settings.TwitchClientSecret);
            var twitchUser = await twitch.GetUserAsync();
            await this.sessionInfoProvider.SetTwitchUserAsync(HttpContext.GetSessionId(), twitchUser);
            return twitchUser;
        }

        private async Task<TwitchAuth> TwitchAuthenticateAsync(string code)
        {
            /*
            POST https://id.twitch.tv/oauth2/token
                ?client_id=<your client ID>
                &client_secret=<your client secret>
                &code=<authorization code received above>
                &grant_type=authorization_code
                &redirect_uri=<your registered redirect URI>
             */

#if DEBUG
            Dictionary<string, string> parameters = null;
            if (HttpContext != null && HttpContext.Request != null)
            {
                if (HttpContext.Request.Host.Value.ToLower().Contains("localhost"))
                {
                    parameters = new Dictionary<string, string> {
                        { "client_id", settings.TwitchClientId },
                        { "client_secret", settings.TwitchClientSecret },
                        { "code", code },
                        { "grant_type","authorization_code" },
                        { "redirect_uri", "https://localhost:5001/api/twitch/authorize"}
                    };
                }
            }

            if (parameters == null)
            {
                parameters = new Dictionary<string, string> {
                    { "client_id", settings.TwitchClientId },
                    { "client_secret", settings.TwitchClientSecret },
                    { "code", code },
                    { "grant_type","authorization_code" },
                    { "redirect_uri", "https://www.ravenfall.stream/api/twitch/authorize"}
                };
            }
#else
            var parameters = new Dictionary<string, string> {
                { "client_id", settings.TwitchClientId },
                { "client_secret", settings.TwitchClientSecret },
                { "code", code },
                { "grant_type","authorization_code" },
                { "redirect_uri", "https://www.ravenfall.stream/api/twitch/authorize"}
            };
#endif

            var reqUrl = "https://id.twitch.tv/oauth2/token?"
                + string.Join("&", parameters.Select(x => x.Key + "=" + x.Value));

            var req = (HttpWebRequest)HttpWebRequest.Create(reqUrl);
            req.Method = "POST";
            req.Accept = "application/vnd.twitchtv.v5+json";

            try
            {
                using (var res = await req.GetResponseAsync())
                using (var stream = res.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    return JsonConvert.DeserializeObject<TwitchAuth>(await reader.ReadToEndAsync());
                }
            }
            catch (WebException we)
            {
                var resp = we.Response as HttpWebResponse;
                if (resp != null)
                {
                    using (var stream = resp.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                    {
                        var errorText = await reader.ReadToEndAsync();
                        System.IO.File.AppendAllText("request-error.log", errorText);
                    }
                }

                throw;
            }
        }

        private bool TryGetSession(out SessionInfo sessionInfo)
        {
            //            var twitchToken = GetExtensionToken();
            //            if (twitchToken == null)
            //            {
            //#if !DEBUG
            //                sessionInfo = null;
            //                return false;
            //#endif
            //            }

            var sessionId = HttpContext.GetSessionId();
            sessionInfoProvider.TryGet(sessionId, out sessionInfo);
            return sessionInfo != null;
        }

        private string GetExtensionToken()
        {
            if (HttpContext.Request.Headers.TryGetValue("rf-twitch-token", out var value))
            {
                return value;
            }
            return null;
        }

        private AuthToken GetAuthToken()
        {
            if (HttpContext.Request.Headers.TryGetValue("auth-token", out var value))
            {
                return authManager.Get(value);
            }
            return null;
        }
    }
}
