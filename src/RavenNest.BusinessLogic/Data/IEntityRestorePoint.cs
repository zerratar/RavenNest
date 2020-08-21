using RavenNest.DataModels;
using System;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Data
{
    public interface IEntityRestorePoint
    {
        IReadOnlyList<Type> GetEntityTypes();
        IReadOnlyList<T> Get<T>() where T : IEntity;
    }
}