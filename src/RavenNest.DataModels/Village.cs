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
    }
}
