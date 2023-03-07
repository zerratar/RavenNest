using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class HighscoreService
    {
        private readonly TimeSpan cacheLifeTime = TimeSpan.FromSeconds(10);
        private readonly HighScoreManager highScoreManager;
        private readonly ConcurrentDictionary<CacheKey, CacheItem<HighScoreCollection>> cache
            = new ConcurrentDictionary<CacheKey, CacheItem<HighScoreCollection>>();
        public HighscoreService(HighScoreManager highScoreManager)
        {
            this.highScoreManager = highScoreManager;
        }

        private HighScoreCollection GetHighscore(int offset, int take, int characterIndex)
        {
            if (TryGetCachedResult("all", offset, take, characterIndex, out HighScoreCollection result))
            {
                return result;
            }

            result = highScoreManager.GetHighScore(offset, take, characterIndex);

            return UpdateCache("all", offset, take, characterIndex, result);
        }

        private HighScoreCollection GetHighscore(string skill, int offset, int take, int characterIndex)
        {
            // this seem to be called twice in a row every time
            // so we will temporary cache requests based on input values.

            if (TryGetCachedResult(skill, offset, take, characterIndex, out HighScoreCollection result))
            {
                return result;
            }

            result = highScoreManager.GetSkillHighScore(skill, offset, take, characterIndex);

            return UpdateCache(skill, offset, take, characterIndex, result);
        }

        public async Task<HighScoreCollection> GetHighscoreAsync(int offset, int take, int characterIndex)
        {
            return await Task.Run(() => GetHighscore(offset, take, characterIndex));
        }

        public async Task<HighScoreCollection> GetHighscoreAsync(string skill, int offset, int take, int characterIndex)
        {
            return await Task.Run(() => GetHighscore(skill, offset, take, characterIndex));
        }

        private bool TryGetCachedResult(string skill, int offset, int take, int characterIndex, out HighScoreCollection result)
        {
            result = null;
            var key = GetCacheKey(skill, offset, take, characterIndex);
            if (cache.TryGetValue(key, out var res) && (DateTime.UtcNow - res.Created) <= cacheLifeTime)
            {
                result = res.Data;
                return true;
            }

            return false;
        }

        private HighScoreCollection UpdateCache(string skill, int offset, int take, int characterIndex, HighScoreCollection result)
        {
            var key = GetCacheKey(skill, offset, take, characterIndex);
            cache[key] = new CacheItem<HighScoreCollection>(result);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CacheKey GetCacheKey(string skill, int offset, int take, int characterIndex)
        {
            return new CacheKey(skill, offset, take, characterIndex);
        }

        private struct CacheItem<T>
        {
            public readonly DateTime Created;
            public readonly T Data;

            public CacheItem(T data)
            {
                Created = DateTime.UtcNow;
                Data = data;
            }
        }

        private struct CacheKey
        {
            public readonly string Skill;
            public readonly int Offset;
            public readonly int Take;
            public readonly int CharacterIndex;
            public CacheKey(string skill, int offset, int take, int characterIndex) : this()
            {
                Skill = skill;
                Offset = offset;
                Take = take;
                CharacterIndex = characterIndex;
            }

            public override bool Equals(object obj)
            {
                return obj is CacheKey key &&
                       Skill == key.Skill &&
                       Offset == key.Offset &&
                       Take == key.Take &&
                       CharacterIndex == key.CharacterIndex;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Skill, Offset, Take, CharacterIndex);
            }
        }
    }
}
