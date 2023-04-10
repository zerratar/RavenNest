using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace RavenNest.DataModels
{
    public class EntitySet<TModel> : IEntitySet<TModel> where TModel : Entity<TModel>
    {
        private readonly ConcurrentDictionary<Guid, TModel> entities;
        private readonly ConcurrentDictionary<Guid, EntityChangeSet> addedEntities;
        private readonly ConcurrentDictionary<Guid, EntityChangeSet> updatedEntities;
        private readonly ConcurrentDictionary<Guid, EntityChangeSet> removedEntities;
        private readonly ConcurrentDictionary<string, EntityLookupGroup<TModel>> groupLookup = new();
        private readonly bool trackChanges;
        public IReadOnlyList<TModel> Entities => entities.Values.AsReadOnlyList();
        public IReadOnlyList<EntityChangeSet> Added => addedEntities.Values.AsReadOnlyList();
        public IReadOnlyList<EntityChangeSet> Updated => updatedEntities.Values.AsReadOnlyList();
        public IReadOnlyList<EntityChangeSet> Removed => removedEntities.Values.AsReadOnlyList();

        public DateTime LastModified { get; private set; }

        public EntitySet(bool trackChanges = true)
        {
            this.trackChanges = trackChanges;
            this.entities = new ConcurrentDictionary<Guid, TModel>();
            this.addedEntities = new ConcurrentDictionary<Guid, EntityChangeSet>();
            this.updatedEntities = new ConcurrentDictionary<Guid, EntityChangeSet>();
            this.removedEntities = new ConcurrentDictionary<Guid, EntityChangeSet>();
        }

        public EntitySet(IEnumerable<TModel> collection, bool trackChanges = true)
        {
            this.trackChanges = trackChanges;
            this.entities = new ConcurrentDictionary<Guid, TModel>();
            this.addedEntities = new ConcurrentDictionary<Guid, EntityChangeSet>();
            this.updatedEntities = new ConcurrentDictionary<Guid, EntityChangeSet>();
            this.removedEntities = new ConcurrentDictionary<Guid, EntityChangeSet>();

            foreach (var entity in collection)
            {
                if (trackChanges)
                {
                    entity.PropertyChanged += OnEntityPropertyChanged;
                }
                entities[entity.Id] = entity;
            }
        }

        // This is okay, its only used for saving backups or restoring from a backup.        
        public IReadOnlyList<IEntity> GetEntities()
        {
            return Entities.AsList(x => (IEntity)x);
        }

        public Type GetEntityType()
        {
            return typeof(TModel);
        }

        public void ClearChanges()
        {
            addedEntities.Clear();
            updatedEntities.Clear();
            removedEntities.Clear();
        }

        public bool TryGet(Guid key, out TModel entity)
        {
            return entities.TryGetValue(key, out entity);
        }

        public bool Contains(Guid key)
        {
            return entities.ContainsKey(key);
        }

        public TModel this[Guid key]
        {
            get
            {
                if (TryGet(key, out var model))
                {
                    return model;
                }

                return null;
                //throw new KeyNotFoundException("No entities with the ID " + key + " could be found.");
            }
        }

        public IReadOnlyList<TModel> this[string group, Guid groupKey]
        {
            get
            {
                if (this.groupLookup.TryGetValue(group, out var groupEntities))
                {
                    var grouped = groupEntities[groupKey];
                    if (grouped != null)
                        return grouped.Values.AsList();
                    return new List<TModel>();
                }

                return new List<TModel>();
                //throw new KeyNotFoundException("No entities could be found with the provided ID.");
            }
        }

        public TModel this[string group, Guid groupKey, Guid itemKey]
        {
            get
            {
                if (this.groupLookup.TryGetValue(group, out var groupEntities))
                {
                    return groupEntities[groupKey, itemKey];
                }

                return null;
                //throw new KeyNotFoundException("No entities could be found with the provided ID.");
            }
        }

        public AddEntityResult Add(TModel model)
        {
            var key = model.Id;

            if (addedEntities.ContainsKey(key) || updatedEntities.ContainsKey(key))
                return AddEntityResult.AlreadyAdded;

            if (removedEntities.ContainsKey(key))
            {
                // so item was removed but added again.
                // could this be related to moving an item and trying to use the same ID?
                // this is appearant when the "item missing bug" occurs.
                return AddEntityResult.AlreadyRemoved;
            }

            if (entities.ContainsKey(key))
                return AddEntityResult.AlreadyExists;


            LastModified = DateTime.UtcNow;

            model.PropertyChanged -= OnEntityPropertyChanged;
            model.PropertyChanged += OnEntityPropertyChanged;

            foreach (var group in this.groupLookup)
            {
                group.Value.Add(model);
            }

            entities[key] = model;

            if (trackChanges)
            {
                addedEntities[key] = new EntityChangeSet
                {
                    LastModified = DateTime.UtcNow,
                    State = EntityState.Added,
                    Entity = model
                };
            }

            return AddEntityResult.Success;
        }

        public RemoveEntityResult Remove(TModel model)
        {
            var key = model.Id;
            if (entities.TryRemove(key, out _))
            {
                LastModified = DateTime.UtcNow;
                model.PropertyChanged -= OnEntityPropertyChanged;

                foreach (var group in this.groupLookup)
                {
                    group.Value.Remove(model);
                }

                if (trackChanges)
                {
                    if (addedEntities.TryRemove(key, out _))
                    {
                        return RemoveEntityResult.Success;
                    }

                    updatedEntities.TryRemove(key, out _);
                    removedEntities[key] = new EntityChangeSet
                    {
                        LastModified = DateTime.UtcNow,
                        State = EntityState.Deleted,
                        Entity = model
                    };
                }

                return RemoveEntityResult.Success;
            }

            return RemoveEntityResult.DoesNotExist;
        }

        //public void Update(TModel model)
        //{
        //    var key = keySelector(model);
        //    if (addedEntities.ContainsKey(key) || removedEntities.ContainsKey(key))
        //    {
        //        return; // dont do anything
        //    }
        //    updatedEntities
        //}

        public void AddRange(IEnumerable<TModel> models) => models.ForEach(x => { Add(x); });
        public void RemoveRange(IEnumerable<TModel> models) => models.ForEach(x => { Remove(x); });
        //public void UpdateRange(IEnumerable<TModel> models) => RavenNest.Models.ForEach(Update);        

        //public ConcurrentQueue<EntityChangeSet<TModel>> BuildAddQueue()
        //{
        //    var queue = new ConcurrentQueue<EntityChangeSet<TModel>>();
        //    return queue;
        //}

        private void OnEntityPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            LastModified = DateTime.UtcNow;
            var entity = sender as TModel;
            //var property = e.PropertyName;
            var key = entity.Id;

            // check if a group key has changed and if so, re-evaluate which group it should contain to.
            foreach (var group in groupLookup)
            {
                group.Value.Update(entity);
            }

            if (trackChanges)
            {
                if (addedEntities.ContainsKey(key) || removedEntities.ContainsKey(key)) return;
                if (updatedEntities.TryGetValue(key, out var value))
                {
                    value.LastModified = DateTime.UtcNow;
                    value.State = EntityState.Modified;
                    return;
                }

                updatedEntities[key] = new EntityChangeSet
                {
                    LastModified = DateTime.UtcNow,
                    State = EntityState.Modified,
                    Entity = entity
                };
            }
        }

        public void RegisterLookupGroup(string name, Func<TModel, Guid> lookupKey)
        {
            this.groupLookup[name] = new EntityLookupGroup<TModel>(
                new ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, TModel>>(
                    entities.Values.GroupBy(lookupKey).ToDictionary(x => x.Key, x =>
                    {
                        var dictionary = x.ToDictionary(y => y.Id, y => y);
                        return new ConcurrentDictionary<Guid, TModel>(dictionary);
                    })),
                lookupKey);
        }

        public void Clear(IReadOnlyList<IEntity> entities)
        {
            foreach (var entity in entities)
            {
                if (!(entity is TModel model)) continue;
                var key = model.Id;
                this.addedEntities.TryRemove(key, out _);
                this.updatedEntities.TryRemove(key, out _);
                this.removedEntities.TryRemove(key, out _);
            }
        }
    }

    public enum AddEntityResult
    {
        Success,
        AlreadyAdded,
        AlreadyExists,
        AlreadyRemoved,
        Error
    }
    public enum RemoveEntityResult
    {
        Success,
        AlreadyRemoved,
        DoesNotExist,
        Error
    }
    public enum EntityState
    {
        Unchanged,
        Added,
        Modified,
        Deleted
    }
}
