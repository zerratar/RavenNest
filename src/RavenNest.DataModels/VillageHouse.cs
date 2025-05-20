using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class VillageHouse : Entity<VillageHouse>
    {
        [PersistentData] private Guid villageId;
        [PersistentData] private Guid? userId;
        [PersistentData] private Guid? characterId;
        [PersistentData] private int slot;
        [PersistentData] private int type;
        [PersistentData] private DateTime created;
    }
}
