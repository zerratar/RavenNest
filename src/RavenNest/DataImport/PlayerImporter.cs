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

        public async Task<string> ImportJsonDatabaseAsync()
        {
            var itemRepo = new PlayerRepository("E:\\git\\Ravenfall\\Data\\Repositories");

            try
            {
                var db = dbProvider.Get();
                var zerratar = await db.User.FirstOrDefaultAsync(x => x.UserId == "72424639");

                if (zerratar == null)
                {
                    zerratar = new User();
                    zerratar.Id = Guid.NewGuid();
                    zerratar.UserId = "72424639";
                    zerratar.UserName = "zerratar";
                    zerratar.Created = DateTime.UtcNow;
                    await db.User.AddAsync(zerratar);
                }

                var players = itemRepo.All();

                foreach (var player in players)
                {
                    if (player.Name.StartsWith("Player ")) continue;
                    if (string.IsNullOrEmpty(player.UserId)) continue; // MUST HAVE A USERID!!

                    var user = await db.User
                        .Include(x => x.CharacterUser)
                        .FirstOrDefaultAsync(x => x.UserId == player.UserId);

                    if (user == null)
                    {
                        user = new User
                        {
                            Id = Guid.NewGuid(),
                            UserName = player.Name,
                            UserId = player.UserId,
                            Created = DateTime.UtcNow
                        };
                        await db.User.AddAsync(user);
                    }

                    if (user.CharacterUser?.FirstOrDefault(x => !x.Local) != null)
                    {
                        continue; // we already have a global character
                    }

                    var resources = new Resources
                    {
                        Id = Guid.NewGuid(),
                        Coins = (decimal)player.SkillResources.Coins.Value,
                        Ore = (decimal)player.SkillResources.Mining.Value,
                        Wheat = (decimal)player.SkillResources.Farming.Value,
                        Fish = (decimal)player.SkillResources.Fishing.Value,
                        Wood = (decimal)player.SkillResources.Woodcutting.Value
                    };
                    await db.Resources.AddAsync(resources);

                    var skills = new Skills
                    {
                        Id = Guid.NewGuid(),
                        Attack = player.CombatStats.Attack.Experience,
                        Defense = player.CombatStats.Defense.Experience,
                        Strength = player.CombatStats.Strength.Experience,
                        Health = player.CombatStats.Health.Experience,
                        Woodcutting = player.SkillStats.Woodcutting.Experience,
                        Fishing = player.SkillStats.Fishing.Experience,
                        Mining = player.SkillStats.Mining.Experience,
                        Crafting = player.SkillStats.Crafting.Experience,
                        Cooking = player.SkillStats.Cooking.Experience,
                        Farming = player.SkillStats.Farming.Experience,
                        Sailing = player.SkillStats.Sailing.Experience
                    };
                    await db.Skills.AddAsync(skills);

                    var appearance = DataMapper.Map<DataModels.Appearance, PlayerAppearanceDefinition>(player.Appearance);
                    appearance.Id = Guid.NewGuid();
                    await db.Appearance.AddAsync(appearance);

                    var statistics = DataMapper.Map<DataModels.Statistics, Statistics>(player.Statistics);
                    statistics.Id = Guid.NewGuid();
                    await db.Statistics.AddAsync(statistics);

                    var character = new Character
                    {
                        Id = Guid.NewGuid(),
                        StatisticsId = statistics.Id,
                        Statistics = statistics,
                        OriginUserId = zerratar.Id,
                        OriginUser = zerratar,
                        UserId = user.Id,
                        User = user,
                        Created = DateTime.UtcNow,
                        Revision = 0,
                        Name = player.Name,
                        Resources = resources,
                        ResourcesId = resources.Id,
                        Appearance = appearance,
                        AppearanceId = appearance.Id,
                        Skills = skills,
                        SkillsId = skills.Id,
                        Local = false,
                    };

                    await db.Character.AddAsync(character);

                    foreach (var equip in player.Inventory.Equipped)
                    {
                        var invItem = new DataModels.InventoryItem
                        {
                            Id = Guid.NewGuid(),
                            ItemId = equip.Id,
                            Character = character,
                            CharacterId = character.Id,
                            Amount = 1,
                            Equipped = true
                        };

                        await db.InventoryItem.AddAsync(invItem);
                    }

                    foreach (var inv in player.Inventory.Backpack)
                    {
                        var invItem = new DataModels.InventoryItem
                        {
                            Id = Guid.NewGuid(),
                            ItemId = inv.Id,
                            Character = character,
                            CharacterId = character.Id,
                            Amount = 1,
                            Equipped = false
                        };

                        await db.InventoryItem.AddAsync(invItem);
                    }

                    await db.SaveChangesAsync();
                }

                return "yes";
            }
            catch (Exception exc)
            {
                return "no";
            }
        }
    }
}
