using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace RavenNest.BusinessLogic.Providers
{
    public class MemoryFileCacheProvider : IMemoryFileCacheProvider
    {
        private readonly IOptions<MemoryCacheOptions> options;
        private readonly IMemoryCache memoryCache;

        public MemoryFileCacheProvider(IMemoryCache memoryCache)
        {
            this.memoryCache = memoryCache;
        }

        public IMemoryFileCache Get(string key, string extension = ".bin")
        {
            return new MemoryFileCache(memoryCache, key, extension);
        }
    }
}
