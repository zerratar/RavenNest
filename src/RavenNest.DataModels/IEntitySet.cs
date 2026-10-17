using System;
using System.Collections.Generic;

namespace RavenNest.DataModels
{
    public interface IEntitySet
    {
        IReadOnlyList<EntityChangeSet> Added { get; }
        IReadOnlyList<EntityChangeSet> Updated { get; }
        IReadOnlyList<EntityChangeSet> Removed { get; }
        DateTime LastModified { get; }
        void ClearChanges();
        void Clear(IReadOnlyList<IEntity> entities);

        /// <summary>
        /// Clears the changes that were written, each only if it has not changed since (its version is still the one
        /// the save took); a new entity changed while its insert ran is kept as an update for the next save.
        /// </summary>
        void ClearSaved(IReadOnlyList<SavedChange> saved);
        IReadOnlyList<IEntity> GetEntities();
        Type GetEntityType();
        string GetName();
        string GetFullName();
    }

    /// <summary>A change a save took, and its version then.</summary>
    public readonly struct SavedChange
    {
        public SavedChange(EntityChangeSet change, long version)
        {
            Change = change;
            Version = version;
        }

        public EntityChangeSet Change { get; }
        public long Version { get; }
    }

    public interface IEntitySet<TModel> : IEntitySet
    {
        IReadOnlyList<TModel> Entities { get; }
        AddEntityResult Add(TModel model);
        RemoveEntityResult Remove(TModel model);
        //void Update(TModel model); // handle updates automatically, requires Entity<T> to be used.

        void AddRange(IEnumerable<TModel> models);
        void RemoveRange(IEnumerable<TModel> models);
        bool TryGet(Guid key, out TModel entity);
        TModel this[Guid key] { get; }
    }
}
