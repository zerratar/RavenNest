using System.Collections.Generic;
using RavenNest.DataModels;

public class EntityStoreItems
{
    public EntityStoreItems(RavenNest.DataModels.EntityState state, IReadOnlyList<IEntity> entities)
    {
        State = state;
        Entities = entities;
    }

    public RavenNest.DataModels.EntityState State { get; }
    public IReadOnlyList<IEntity> Entities { get; }
}