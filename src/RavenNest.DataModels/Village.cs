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

        // The town hall tier the village has, 0 (the longhouse) to 4 (the castle). Raised only by buying
        // the next tier. Null until a client that knows about upgrades asks; the server then stores the
        // tier the village level gave it before. Requires sql/village-town-hall-tier.sql.
        [PersistentData] private int? townHallTier;
    }
}
