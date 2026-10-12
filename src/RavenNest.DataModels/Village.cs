using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class Village : Entity<Village>
    {
        [PersistentData] private Guid userId;
        [PersistentData] private string name;
        [PersistentData] private int level;
        [PersistentData] private double experience;
        [PersistentData] private Guid resourcesId;

        // Where the streamer moved the town hall in build mode. Null means the plaza in the game scene,
        // where every town hall stood before it could be moved. Requires sql/village-town-hall.sql.
        [PersistentData] private double? townHallX;
        [PersistentData] private double? townHallZ;
        [PersistentData] private double? townHallRotation;
    }
}
