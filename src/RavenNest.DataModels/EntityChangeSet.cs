using System;
using Microsoft.EntityFrameworkCore;

namespace RavenNest.DataModels
{
    public class EntityChangeSet
    {
        public DateTime LastModified { get; set; }
        public EntityState State { get; set; }
        public IEntity Entity { get; set; }
        // could list property changes here
        // if we only want to do updates on those properties
        // in the database.
    }
}