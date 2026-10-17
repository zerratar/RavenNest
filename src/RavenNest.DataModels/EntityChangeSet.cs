using System;

namespace RavenNest.DataModels
{
    public class EntityChangeSet
    {
        public DateTime LastModified { get; set; }
        public EntityState State { get; set; }
        public IEntity Entity { get; set; }

        /// <summary>
        /// Goes up by one with every change, so a save can tell whether the entity changed again while it was being
        /// written: only then is the change left for the next save instead of being cleared (it used to be cleared
        /// by key, losing whatever changed mid-write).
        /// </summary>
        public long Version;
        // could list property changes here
        // if we only want to do updates on those properties
        // in the database.
    }
}