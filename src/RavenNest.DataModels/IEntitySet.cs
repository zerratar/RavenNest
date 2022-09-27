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
        IReadOnlyList<IEntity> GetEntities();
        Type GetEntityType();
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
