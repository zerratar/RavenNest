using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace RavenNest.DataModels
{
    public class EntityLookupGroup<TModel> where TModel : IEntity
    {
        private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, TModel>> entities;
        private readonly Func<TModel, Guid> lookupKey;

        public EntityLookupGroup(ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, TModel>> entities, Func<TModel, Guid> lookupKey)
        {
            this.entities = entities;
            this.lookupKey = lookupKey;
        }

        public ConcurrentDictionary<Guid, TModel> this[Guid key]
        {
            get
            {
                if (entities.TryGetValue(key, out var dict)) return dict;
                return null;
            }
        }

        public TModel this[Guid group, Guid item] => entities[group][item];

        public void Add(TModel entity)
        {
            var groupKey = lookupKey(entity);
            var key = entity.Id;

            if (!entities.ContainsKey(groupKey))
            {
                entities[groupKey] = new ConcurrentDictionary<Guid, TModel>();
            }

            if (entities.TryGetValue(groupKey, out var dict))
            {
                dict[key] = entity;
            }
        }

        public void Remove(TModel entity)
        {
            var groupKey = lookupKey(entity);
            var key = entity.Id;
            entities[groupKey].Remove(key, out _);
        }

        public void Update(TModel entity)
        {
            var groupKey = lookupKey(entity);
            var key = entity.Id;
            if (!entities.ContainsKey(groupKey) || !entities[groupKey].ContainsKey(key))
            {
                MoveEntitySlow(entity);
            }
        }

        private void MoveEntitySlow(TModel entity)
        {
            var groupFound = false;
            var oldKey = Guid.Empty;
            foreach (var groups in entities)
            {
                foreach (var value in groups.Value)
                {
                    if (object.ReferenceEquals(value.Value, entity))
                    {
                        oldKey = value.Key;
                        groupFound = true;
                        break;
                    }
                }

                if (groupFound)
                {
                    groups.Value.TryRemove(oldKey, out _);
                    Add(entity);
                    return;
                }
            }
        }
    }
}
