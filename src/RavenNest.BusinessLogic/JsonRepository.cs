using Newtonsoft.Json;
using RavenNest.BusinessLogic.Tv;
using RavenNest.DataModels;
using RavenNest.Models.Tv;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic
{
    public class JsonRepository<T>
    {
        private readonly string _folderPath;
        private readonly ConcurrentDictionary<Guid, T> _cache;

        public JsonRepository(string folderPath)
        {
            _folderPath = folderPath;
            _cache = new ConcurrentDictionary<Guid, T>();

            if (!Directory.Exists(_folderPath))
            {
                Directory.CreateDirectory(_folderPath);
            }
            else
            {
                LoadAllFilesIntoCache();
            }
        }

        private void LoadAllFilesIntoCache()
        {
            foreach (var filePath in Directory.GetFiles(_folderPath, "*.json"))
            {
                var jsonString = File.ReadAllText(filePath);
                var id = GetIdFromFileName(Path.GetFileName(filePath));
                var obj = JsonConvert.DeserializeObject<T>(jsonString);

                _cache.TryAdd(id, obj);
            }
        }

        public bool Contains(Guid id)
        {
            return _cache.ContainsKey(id);
        }

        public async Task<T> GetAsync(Guid id)
        {
            if (_cache.TryGetValue(id, out var obj))
            {
                return obj;
            }

            var filePath = GetFilePath(id);

            if (!File.Exists(filePath)) return default(T);

            var jsonString = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);

            obj = JsonConvert.DeserializeObject<T>(jsonString);

            _cache.TryAdd(id, obj);

            return obj;
        }

        public async Task SaveAsync(Guid id, T obj)
        {
            var filePath = GetFilePath(id);

            _cache.AddOrUpdate(id, obj, (key, oldValue) => obj);
            var jsonString = JsonConvert.SerializeObject(obj, Formatting.Indented);
            await File.WriteAllTextAsync(filePath, jsonString);
        }

        public Task DeleteAsync(Guid id)
        {
            var filePath = GetFilePath(id);

            if (_cache.TryRemove(id, out _))
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }

            return Task.CompletedTask;
        }

        public async Task<T> GetOneAsync()
        {
            return _cache.FirstOrDefault().Value;
        }

        public List<T> TakeWhereOrdered<T2>(Func<T, bool> wherePredicate, Func<T, T2> orderBy, int take)
        {
            return _cache.Values.Where(wherePredicate).OrderBy(orderBy).Take(take).ToList();
        }

        private string GetFilePath(Guid id)
        {
            return Path.Combine(_folderPath, $"{id}.json");
        }

        private Guid GetIdFromFileName(string fileName)
        {
            return Guid.Parse(Path.GetFileNameWithoutExtension(fileName));
        }

        internal IEnumerable<T> OrderedBy<T2>(Func<T, T2> value)
        {
            return _cache.Values.OrderBy(value);
        }

        internal List<T> TakeRandomWhere(Func<T, bool> value, int take)
        {
            return _cache.Values.Where(value).OrderBy(x => System.Random.Shared.Next()).Take(take).ToList();
        }
    }
}
