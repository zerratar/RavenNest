using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class UserProperty : Entity<UserProperty>
    {
        [PersistentData] private Guid userId;
        [PersistentData] private string propertyKey;
        [PersistentData] private string value;
        [PersistentData] private DateTime? updated;
        [PersistentData] private DateTime created;
    }
}
