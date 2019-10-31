using System;
using System.Collections.Generic;

namespace RavenNest.DataModels
{
    public interface IEntitySet<TModel, TKey>
    {
        ICollection<TModel> Entities { get; }

        ICollection<EntityChangeSet> Added { get; }
        ICollection<EntityChangeSet> Updated { get; }
        ICollection<EntityChangeSet> Removed { get; }

        DateTime LastModified { get; }

        void Add(TModel model);
        void Remove(TModel model);
        //void Update(TModel model); // handle updates automatically, requires Entity<T> to be used.

        void AddRange(IEnumerable<TModel> models);
        void RemoveRange(IEnumerable<TModel> models);
        bool TryGet(TKey key, out TModel entity);

        TModel this[TKey key] { get; }
    }
}