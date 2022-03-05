using Microsoft.Extensions.Caching.Memory;
using System.Runtime.CompilerServices;

namespace RavenNest.BusinessLogic.Providers
{
    public class MemoryFileCache : IMemoryFileCache
    {
        private readonly string relativePath;
        private readonly string extension;
        private readonly IMemoryCache memoryCache;

        public MemoryFileCache(IMemoryCache memoryCache, string relativePath, string extension = ".bin")
        {
            this.extension = extension;
            this.memoryCache = memoryCache;
            this.relativePath = System.IO.Path.Combine("cache", relativePath);

            EnsureCacheFolder();
            LoadCache();
        }

        private void EnsureCacheFolder()
        {
            if (!System.IO.Directory.Exists(relativePath))
            {
                System.IO.Directory.CreateDirectory(relativePath);
            }
        }

        private void LoadCache()
        {
            var cacheFiles = System.IO.Directory.GetFiles(this.relativePath, "*" + this.extension);
            foreach (var f in cacheFiles)
            {
                memoryCache.Set(GetKey(f), System.IO.File.ReadAllBytes(f));
            }
        }

        public void Remove(string key)
        {
            memoryCache.Remove(key);

            var path = GetPath(key);
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }

        public byte[] Set(string key, byte[] fileContent)
        {
            memoryCache.Set(key, fileContent);
            EnsureCacheFolder();
            var path = GetPath(key);
            System.IO.File.WriteAllBytes(path, fileContent);
            return fileContent;
        }

        public bool TryGetValue(string key, out byte[] fileContent)
        {
            return memoryCache.TryGetValue(key, out fileContent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GetKey(string path)
        {
            return System.IO.Path.GetFileNameWithoutExtension(path);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GetPath(string key)
        {
            return System.IO.Path.Combine(this.relativePath, key + this.extension);
        }
    }
}
