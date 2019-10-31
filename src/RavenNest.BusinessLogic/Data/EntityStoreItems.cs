using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using RavenNest.DataModels;

public class EntityStoreItems
{
    public EntityStoreItems(EntityState state, IEnumerable<IEntity> entities)
    {
        State = state;
        Entities = entities;
    }

    public EntityState State { get; }
    public IEnumerable<IEntity> Entities { get; }
}