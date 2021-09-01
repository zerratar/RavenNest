using System;

namespace RavenNest.DataModels
{
    public class UserProperty : Entity<UserProperty>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid userId; public Guid UserId { get => userId; set => Set(ref userId, value); }
        private string propertyKey; public string PropertyKey { get => propertyKey; set => Set(ref propertyKey, value); }
        private string value; public string Value { get => value; set => Set(ref this.value, value); }
        private DateTime? updated; public DateTime? Updated { get => updated; set => Set(ref updated, value); }
        private DateTime created; public DateTime Created { get => created; set => Set(ref created, value); }
    }
}
