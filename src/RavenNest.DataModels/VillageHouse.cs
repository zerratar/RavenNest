using System;

namespace RavenNest.DataModels
{
    public partial class VillageHouse : Entity<VillageHouse>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid villageId; public Guid VillageId { get => villageId; set => Set(ref villageId, value); }
        private Guid? userId; public Guid? UserId { get => userId; set => Set(ref userId, value); }
        private int slot; public int Slot { get => slot; set => Set(ref slot, value); }
        private int type; public int Type { get => type; set => Set(ref type, value); }
        private DateTime created; public DateTime Created { get => created; set => Set(ref created, value); }
    }


    public class UserNotification : Entity<UserNotification>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid userId; public Guid UserId { get => userId; set => Set(ref userId, value); }
        private string icon; public string Icon { get => icon; set => Set(ref icon, value); }
        private string title; public string Title { get => title; set => Set(ref title, value); }
        private string description; public string Description { get => description; set => Set(ref description, value); }
        private string redirectUrl; public string RedirectUrl { get => redirectUrl; set => Set(ref redirectUrl, value); }
        private bool hasRead; public bool HasRead { get => hasRead; set => Set(ref hasRead, value); }
        private DateTime time; public DateTime Time { get => time; set => Set(ref time, value); }
    }
}
