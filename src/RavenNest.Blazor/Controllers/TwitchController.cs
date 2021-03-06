﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
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
        }

        [HttpGet("authorize")]
        public async Task<ActionResult> OAuthAuthorize()
        {
            var reqCode = HttpContext.Request.Query["code"];
            var reqState = HttpContext.Request.Query["state"];
#if DEBUG
            var requestUrl = $"https://{HttpContext.Request.Host}/login/twitch";
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
        public async Task<ActionResult> GetChannelPictureAsync(string userId)
        {
            try
            {
                var imageData = await logoService.GetChannelPictureAsync(userId);
                if (imageData != null)
                {
                    return File(imageData, "image/png");
                }
            }
            catch { }
            return NotFound();
        }

        [HttpGet("clan-logo/{userId}")]
        public async Task<ActionResult> GetClanLogoAsync(string userId)
        {
            try
            {
                var imageData = await logoService.GetClanLogoAsync(userId);
                if (imageData != null)
                {
                    return File(imageData, "image/png");
                }
            }
            catch { }
            return NotFound();
        }

        [HttpGet("clear-logo/{userId}")]
        public bool ClearClanLogoAsync(string userId)
        {
            try
            {
                var session = this.HttpContext.GetSessionId();
                if (!sessionInfoProvider.TryGet(session, out var sessionInfo))
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
            return result;
        }

        [HttpGet("extension/set-task/{broadcasterId}/{characterId}/{task}")]
        public void SetTask(string broadcasterId, Guid characterId, string task)
        {
            SetTask(broadcasterId, characterId, task, null);
        }

        [HttpGet("extension/set-task/{broadcasterId}/{characterId}/{task}/{taskArgument}")]
        public void SetTask(string broadcasterId, Guid characterId, string task, string taskArgument)
        {
            if (string.IsNullOrEmpty(broadcasterId))
            {
                return;
            }

            var session = this.HttpContext.GetSessionId();
            if (!sessionInfoProvider.TryGet(session, out var sessionInfo))
            {
                return;
            }

            var activeSession = gameData.GetOwnedSessionByUserId(broadcasterId);
            if (activeSession == null)
            {
                return;
            }

            var user = gameData.GetUser(sessionInfo.UserId);
            if (user == null)
            {
                return;
            }

            var myCharacters = gameData.GetCharacters(c => c.UserId == user.Id && c.Id == characterId);
            var character = myCharacters.FirstOrDefault();
            if (character == null)
            {
                return;
            }

            playerManager.SendPlayerTaskToGame(activeSession, character, task, taskArgument);
        }

        [HttpGet("extension/leave/{broadcasterId}/{characterId}")]
        public bool PlayerLeave(string broadcasterId, Guid characterId)
        {
            if (string.IsNullOrEmpty(broadcasterId))
            {
                return false;
            }

            var session = this.HttpContext.GetSessionId();
            if (!sessionInfoProvider.TryGet(session, out var sessionInfo))
            {
                return false;
            }

            var activeSession = gameData.GetOwnedSessionByUserId(broadcasterId);
            if (activeSession == null)
            {
                return false;
            }

            var user = gameData.GetUser(sessionInfo.UserId);
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

            playerManager.SendRemovePlayerFromSessionToGame(character);
            return playerManager.RemovePlayerFromActiveSession(activeSession, characterId);
        }

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

            var session = this.HttpContext.GetSessionId();
            if (sessionInfoProvider.TryGet(session, out var sessionInfo))
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

                var gameEvent = gameData.CreateSessionEvent(
                    GameEventType.PlayerAdd,
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


        [HttpGet("extension/new/{broadcasterId}/{userId}/{username}/{displayName}")]
        public Task<SessionInfo> CreateUserAsync(string broadcasterId, string userId, string username, string displayName)
        {
            if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(username))
                playerManager.CreatePlayerIfNotExists(userId, username, "1");

            return SetExtensionViewer(broadcasterId, userId);
        }

        [HttpGet("extension/{broadcasterId}")]
        public StreamerInfo GetStreamerInfo(string broadcasterId)
        {
            var streamer = gameData.GetUser(broadcasterId);
            var result = new StreamerInfo();
            if (streamer != null)
            {
                result.StreamerUserId = broadcasterId;
                result.StreamerUserName = streamer.UserName;

                var gameSession = gameData.GetOwnedSessionByUserId(streamer.UserId);
                result.IsRavenfallRunning = gameSession != null;
                result.StreamerSessionId = gameSession?.Id;
            }

            return result;
        }

        [HttpGet("extension/{broadcasterId}/{viewerId}")]
        public async Task<SessionInfo> SetExtensionViewer(string broadcasterId, string viewerId)
        {
            var session = this.HttpContext.GetSessionId();
            var result = await sessionInfoProvider.CreateTwitchUserSessionAsync(session, broadcasterId, viewerId);
            return result;
        }

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
            var parameters = new Dictionary<string, string> {
                { "client_id", settings.TwitchClientId },
                { "client_secret", settings.TwitchClientSecret },
                { "code", code },
                { "grant_type","authorization_code" },
                { "redirect_uri", "https://localhost:5001/api/twitch/authorize"}
            };
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
