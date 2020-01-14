using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.DataModels;
using RavenNest.Models;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[ApiDescriptor(Name = "Game API", Description = "Used for handling game sessions and polling game events.")]
    public class GameController : ControllerBase
    {
        private readonly ITwitchClient twitchClient;
        private readonly IGameData gameData;
        private readonly IAuthManager authManager;
        private readonly ISessionManager sessionManager;
        private readonly IGameManager gameManager;
        private readonly ISecureHasher secureHasher;

        public GameController(
            ITwitchClient twitchClient,
            IGameData gameData,
            IAuthManager authManager,
            ISessionManager sessionManager,
            IGameManager gameManager,
            ISecureHasher secureHasher)
        {
            this.twitchClient = twitchClient;
            this.gameData = gameData;
            this.authManager = authManager;
            this.sessionManager = sessionManager;
            this.gameManager = gameManager;
            this.secureHasher = secureHasher;
        }


        [HttpGet("youcrazyone")]
        public bool DoCrazyStuff()
        {
            var characters = gameData.GetCharacters(x => true);
            foreach (var c in characters)
            {
                //var items = gameData.GetInventoryItems(c.Id);
                //foreach (var eq in items)
                //{
                //    if (eq.Amount == 0)
                //    {
                //        gameData.Remove(eq);
                //    }
                //    //var item = gameData.GetItem(eq.ItemId);
                //    //if (item.Category == (int)DataModels.ItemCategory.Resource)
                //    //{
                //    //    var stack = gameData.GetInventoryItem(c.Id, eq.ItemId);
                //    //    if (stack != null)
                //    //    {
                //    //        stack.Amount += eq.Amount;
                //    //        gameData.Remove(eq);
                //    //    }
                //    //    else
                //    //    {
                //    //        eq.Equipped = false;
                //    //    }
                //    //}
                //}
                var items = gameData.GetInventoryItems(c.Id);
                foreach (var stack in items.GroupBy(x => x.ItemId))
                {
                    gameData.RemoveRange(stack.ToList());
                    var newAmount = stack.Sum(x => x.Amount);
                    gameData.Add(new DataModels.InventoryItem
                    {
                        Amount = newAmount,
                        CharacterId = c.Id,
                        Id = Guid.NewGuid(),
                        ItemId = stack.Key,
                        Equipped = false
                    });
                }
            }
            //}
            gameData.Flush();

            return true;
        }

        //[HttpGet("youcrazyone")]
        //public bool DoCrazyStuff()
        //{
        //    var abraxasTokenId = new Guid("0a816d30-a02a-4f8b-8f92-0635cc34f0cb");
        //    var runeTokenId = new Guid("c7391420-1c94-4878-b544-b12ef476cf16");

        //    var marketItems = gameData.GetMarketItems(0, 10000);
        //    foreach (var marketItem in marketItems)
        //    {
        //        var existing = gameData.GetInventoryItem(marketItem.SellerCharacterId, marketItem.ItemId);
        //        if (existing != null)
        //        {
        //            existing.Amount += marketItem.Amount;
        //        }
        //        else
        //        {
        //            gameData.Add(new DataModels.InventoryItem
        //            {
        //                CharacterId = marketItem.SellerCharacterId,
        //                Amount = marketItem.Amount,
        //                ItemId = marketItem.ItemId,
        //                Id = Guid.NewGuid()
        //            });
        //        }
        //        gameData.Remove(marketItem);
        //    }

        //    var characters = gameData.GetCharacters(x => true);
        //    foreach (var c in characters)
        //    {
        //        var resources = gameData.GetResources(c.ResourcesId);
        //        var skills = gameData.GetSkills(c.SkillsId);
        //        if (resources == null || skills == null) continue;

        //        // 2.5xp (5xp every 2 seconds)
        //        var totalSecondsMining = skills.Mining / 2.5m;

        //        // 8.3xp (25xp every 3 seconds)
        //        var totalSecondsFishing = skills.Fishing / 8.3m;

        //        // 8.3xp (25xp every 3 seconds)
        //        var totalSecondsFarming = skills.Farming / 8.3m;

        //        // 12.5xp (25xp every 2 seconds)
        //        var totalSecondsWoodcutting = skills.Woodcutting / 12.5m;

        //        resources.Ore = (long)(totalSecondsMining / 10m);
        //        resources.Fish = (long)(totalSecondsFishing / 10m);
        //        resources.Wood = (long)(totalSecondsWoodcutting / 10m);
        //        resources.Wheat = (long)(totalSecondsFarming / 10m);


        //        while (resources.Coins > 0)
        //        {
        //            if (resources.Coins >= 1_000_000)
        //            {
        //                var aToken = gameData.GetInventoryItem(c.Id, abraxasTokenId);
        //                if (aToken != null)
        //                {
        //                    ++aToken.Amount;
        //                }
        //                else
        //                {
        //                    gameData.Add(new DataModels.InventoryItem
        //                    {
        //                        CharacterId = c.Id,
        //                        Amount = 1,
        //                        ItemId = abraxasTokenId,
        //                        Id = Guid.NewGuid()
        //                    });
        //                }

        //                resources.Coins -= 1_000_000;
        //            }
        //            else if (resources.Coins >= 100_000)
        //            {
        //                var aToken = gameData.GetInventoryItem(c.Id, runeTokenId);
        //                if (aToken != null)
        //                {
        //                    ++aToken.Amount;
        //                }
        //                else
        //                {
        //                    gameData.Add(new DataModels.InventoryItem
        //                    {
        //                        CharacterId = c.Id,
        //                        Amount = 1,
        //                        ItemId = runeTokenId,
        //                        Id = Guid.NewGuid()
        //                    });
        //                }

        //                resources.Coins -= 100_000;
        //            }
        //            else
        //            {
        //                break;
        //            }
        //        }
        //    }

        //    return true;
        //}

        [HttpGet]
        public GameInfo Get()
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return gameManager.GetGameInfo(session);
        }

        [HttpPost("{clientVersion}/{accessKey}")]

        public async Task<SessionToken> BeginSessionAsync(string clientVersion, string accessKey, Single<bool> local)
        {
            var authToken = GetAuthToken();
            AssertAuthTokenValidity(authToken);

            var session = await this.sessionManager.BeginSessionAsync(authToken, clientVersion, accessKey, local.Value);
            if (session == null)
            {
                HttpContext.Response.StatusCode = 403;
                return null;
            }

            return session;
        }

        [HttpDelete("raid/{username}")]
        public bool EndSessionAndRaid(string username, Single<bool> war)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.sessionManager.EndSessionAndRaid(session, username, war.Value);
        }

        [HttpDelete]
        public void EndSession()
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            this.sessionManager.EndSession(session);
        }

        private SessionToken GetSessionToken()
        {
            if (HttpContext.Request.Headers.TryGetValue("session-token", out var value))
            {
                return sessionManager.Get(value);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertAuthTokenValidity(AuthToken authToken)
        {
            if (authToken == null) throw new NullReferenceException(nameof(authToken));
            if (authToken.UserId == Guid.Empty) throw new NullReferenceException(nameof(authToken.UserId));
            if (authToken.Expired) throw new Exception("Session has expired.");
            if (string.IsNullOrEmpty(authToken.Token)) throw new Exception("Session has expired.");
            if (authToken.Token != secureHasher.Get(authToken))
            {
                throw new Exception("Session has expired.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AssertSessionTokenValidity(SessionToken sessionToken)
        {
            if (sessionToken == null) throw new NullReferenceException(nameof(sessionToken));
            if (sessionToken.SessionId == Guid.Empty) throw new NullReferenceException(nameof(sessionToken.SessionId));
            if (sessionToken.Expired) throw new Exception("Session has expired.");
        }
    }

}