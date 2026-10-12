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

        // Where the streamer placed the building in build mode. Null means the plot's original
        // position in the game scene, which is where every house built before placement existed
        // stays until it is moved. Requires sql/village-house-placement.sql.
        [PersistentData] private double? positionX;
        [PersistentData] private double? positionZ;
        [PersistentData] private double? rotation;
    }
}
