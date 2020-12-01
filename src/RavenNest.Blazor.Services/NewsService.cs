using Microsoft.Extensions.Caching.Memory;
using RavenNest.Blazor.Services.RSS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class NewsService
    {
        private readonly IMemoryCache newsCache;
        public NewsService(IMemoryCache memoryCache)
        {
            newsCache = memoryCache;
        }

        public async Task<IReadOnlyList<NewsItem>> GetNewsFeedAsync(int take)
        {
            if (newsCache.TryGetValue<IReadOnlyList<NewsItem>>("news_feed", out var news))
                return news.Take(take).ToList();

            var newsReader = new NewsReader("https://medium.com/feed/ravenfall");
            news = await newsReader.GetNewsAsync();
            return newsCache.Set("news_feed", news, TimeSpan.FromMinutes(30))
                .Take(take)
                .ToList();
        }
    }

}
