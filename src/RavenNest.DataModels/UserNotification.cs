using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class UserNotification : Entity<UserNotification>
    {
        [PersistentData] private Guid userId;
        [PersistentData] private string icon;
        [PersistentData] private string title;
        [PersistentData] private string description;
        [PersistentData] private string redirectUrl;
        [PersistentData] private bool hasRead;
        [PersistentData] private DateTime time;
    }
}
