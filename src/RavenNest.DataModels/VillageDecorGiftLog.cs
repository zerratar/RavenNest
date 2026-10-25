using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>A viewer gave a village a decor piece: who, what and for how many loyalty points.</summary>
    public partial class VillageDecorGiftLog : Entity<VillageDecorGiftLog>
    {
        [PersistentData] private Guid villageId;
        [PersistentData] private string piece;
        [PersistentData] private Guid userId;
        [PersistentData] private string userName;
        [PersistentData] private long points;
        [PersistentData] private DateTime created;
    }
}
