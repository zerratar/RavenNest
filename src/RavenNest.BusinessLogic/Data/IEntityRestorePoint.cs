using RavenNest.DataModels;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Data
{
    public interface IEntityRestorePoint
    {
        IReadOnlyList<T> Get<T>() where T : IEntity;
    }
}