using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.DataModels;

namespace RavenNest
{
    public class PlayerImporter
    {
        private readonly IPlayerManager playerManager;
        private readonly IRavenfallDbContextProvider dbProvider;
        private readonly AppSettings settings;
        private readonly HttpContext httpContext;

        public PlayerImporter(
            IPlayerManager playerManager,
            IRavenfallDbContextProvider dbProvider,
            AppSettings settings,
            HttpContext httpContext)
        {
            this.playerManager = playerManager;
            this.dbProvider = dbProvider;
            this.settings = settings;
            this.httpContext = httpContext;
        }

        public async Task<string> FixMissingUserIds()
        {
            var itemRepo = new PlayerRepository("E:\\git\\Ravenfall\\Data\\Repositories");
            var players = itemRepo.All();

            var key = "";

            if (httpContext != null && httpContext.Session.Keys.Contains("twitch_access_token"))
            {
                var str = httpContext.Session.GetString("twitch_access_token");
                if (!string.IsNullOrEmpty(str))
                {
                    key = str;
                }
            }

            if (string.IsNullOrEmpty(key))
            {
                return "nope";
            }

            var queue = new Queue<PlayerDefinition>(players
                .Where(x => string.IsNullOrEmpty(x.UserId) && !x.Name.StartsWith("Player ")));
            var batchList = new List<PlayerDefinition>();
            var completedList = new List<PlayerDefinition>();
            while (queue.Count > 0 || batchList.Count > 0)
            {
                if (batchList.Count == 100 || (batchList.Count > 0 && queue.Count == 0))
                {
                    var query = string.Join("&", batchList.Select(x => "login=" + x.Name));
                    var response = await TwitchRequestAsync(
                        "https://api.twitch.tv/helix/users?" + query,
                        key);

                    var result = JSON.Parse<TwitchUserListResponse>(response);

                    foreach (var item in batchList)
                    {
                        var info = result.Data.FirstOrDefault(x =>
                            x.Login.Equals(item.Name, StringComparison.InvariantCultureIgnoreCase));
                        if (info == null) continue;
                        item.UserId = info.Id;
                        completedList.Add(item);
                    }

                    batchList.Clear();
                }
                else
                {
                    if (queue.Count == 0)
                    {
                        break;
                    }
                    batchList.Add(queue.Dequeue());
                }
            }

            itemRepo.UpdateMany(completedList);
            itemRepo.Save();

            return "yes";
        }

        public class TwitchUserListResponse
        {
            public List<TwitchUser> Data { get; set; }
        }

        public class TwitchUser
        {
            public string Id { get; set; }
            public string Login { get; set; }
            [JsonProperty("display_name")]
            public string DisplayName { get; set; }
            public string Type { get; set; }
            public string Email { get; set; }
        }

        private async Task<string> TwitchRequestAsync(string url, string accessToken)
        {
            var req = (HttpWebRequest)HttpWebRequest.Create(url);
            req.Method = "GET";
            req.Headers["Authorization"] = $"Bearer {accessToken}";
            using (var res = await req.GetResponseAsync())
            using (var stream = res.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                return await reader.ReadToEndAsync();
            }
        }   
    }
}
