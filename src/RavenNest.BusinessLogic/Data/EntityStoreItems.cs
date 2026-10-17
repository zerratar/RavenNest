using System.Collections.Generic;
using RavenNest.DataModels;

public class EntityStoreItems
{
    public EntityStoreItems(RavenNest.DataModels.EntityState state, IReadOnlyList<IEntity> entities, IReadOnlyList<RavenNest.DataModels.SavedChange> changes = null)
    {
        State = state;
        Entities = entities;
        Changes = changes;
    }

    public RavenNest.DataModels.EntityState State { get; }
    public IReadOnlyList<IEntity> Entities { get; }

    /// <summary>The changes these are, with their versions when the save took them (see EntitySet.ClearSaved).</summary>
    public IReadOnlyList<RavenNest.DataModels.SavedChange> Changes { get; }
}