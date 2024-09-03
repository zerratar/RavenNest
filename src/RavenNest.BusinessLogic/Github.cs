using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Github
{
    public class Github
    {
        private readonly static GitHubReleaseProvider github = new GitHubReleaseProvider();
        public static async Task<GameRelease> GetGithubReleaseAsync()
        {
            return await github.GetGithubReleaseAsync("zerratar", "ravenfall-legacy");
        }
    }

    public class GitHubReleaseProvider
    {
        private static readonly MemoryCache<GameRelease> cachedVersion = new MemoryCache<GameRelease>();
        private readonly static TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public async Task<GameRelease> GetGithubReleaseAsync(string owner, string repo)
        {
            try
            {
                if (cachedVersion.TryGet(owner + "_" + repo, out var updateRes))
                {
                    return updateRes;
                }

                using (var client = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/releases/latest");
                    request.Headers.Add("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
                    request.Headers.Add("user-agent", "Mozilla/5.0 (Linux; Android 6.0; Nexus 5 Build/MRA58N) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/90.0.4430.212 Mobile Safari/537.36");

                    using (var response = await client.SendAsync(request))
                    {
                        var data = await response.Content.ReadAsStringAsync();
                        var result = new GameRelease(Newtonsoft.Json.JsonConvert.DeserializeObject<Root>(data));
                        cachedVersion.Set(owner + "_" + repo, result, CacheDuration);
                        return result;
                    }
                }
            }
            catch (Exception exc)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(exc.ToString());
                Console.ResetColor();
                return null;
            }
        }

        private class MemoryCache<T>
        {
            private readonly ConcurrentDictionary<string, MemoryCacheItem> items =
                new ConcurrentDictionary<string, MemoryCacheItem>();

            public void Set(string key, T value)
            {
                items[key] = new MemoryCacheItem(value);
            }

            public void Set(string key, T value, TimeSpan lifespan)
            {
                items[key] = new MemoryCacheItem(value, lifespan);
            }

            public bool TryGet(string key, out T value)
            {
                value = default;

                if (items.TryGetValue(key, out var item))
                {
                    if (item.Expired)
                    {
                        items.TryRemove(key, out _);
                        return false;
                    }

                    value = item.Item;
                    return true;
                }

                return false;
            }

            private class MemoryCacheItem
            {
                public T Item { get; }
                public DateTime Created { get; }
                public DateTime Expires { get; }
                public bool Expired => DateTime.Now >= Expires;

                public MemoryCacheItem(T item) : this(item, TimeSpan.MaxValue)
                {
                }

                public MemoryCacheItem(T item, TimeSpan lifespan)
                {
                    Item = item;
                    Created = DateTime.Now;
                    Expires = Created + lifespan;
                }
            }
        }
    }

    public class GameRelease
    {
        public GameRelease(Root root)
        {
            this.VersionString = root.TagName.Replace("-alpha", "").Replace("v", "");

            if (RavenNest.BusinessLogic.Game.GameVersion.TryParse(root.TagName, out var gameVersion))
            {
                Version = gameVersion;
            }
            this.Description = ParseDescription(root.Body);
            this.UpdateDownloadUrl_Win = root.Assets.FirstOrDefault(x => x.Name.StartsWith("update."))?.BrowserDownloadUrl;
            this.UpdateDownloadUrl_Linux = root.Assets.FirstOrDefault(x => x.Name.StartsWith("update-linux"))?.BrowserDownloadUrl;
            this.FullDownloadUrl_Win = root.Assets.FirstOrDefault(x => x.Name.StartsWith("Ravenfall.v") && !x.Name.Contains("linux"))?.BrowserDownloadUrl; 
            this.FullDownloadUrl_Linux = root.Assets.FirstOrDefault(x => x.Name.StartsWith("Ravenfall.v") && x.Name.Contains("linux"))?.BrowserDownloadUrl;
            this.Published = root.PublishedAt;
        }

        private string ParseDescription(string body)
        {
            // it is markdown formatted, maybe we should parse this to get changelog
            // or just clean the markdown? for now return the whole body.
            return body;
        }

        public Version Version { get; }
        public string VersionString { get; }
        public string Description { get; }

        public string UpdateDownloadUrl_Win { get; }
        public string FullDownloadUrl_Win { get; }

        public string UpdateDownloadUrl_Linux { get; }
        public string FullDownloadUrl_Linux { get; }
        public DateTime Published { get; }
    }

    public class Asset
    {
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        [Newtonsoft.Json.JsonProperty("browser_download_url")]
        public string BrowserDownloadUrl { get; set; }
    }

    public class Root
    {
        [Newtonsoft.Json.JsonProperty("tag_name")]
        public string TagName { get; set; }
        public string Body { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime PublishedAt { get; set; }
        public List<Asset> Assets { get; set; }
    }
}
