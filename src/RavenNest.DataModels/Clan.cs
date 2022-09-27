using System;

namespace RavenNest.DataModels
{
    public partial class Clan : Entity<Clan>
    {

        private Guid userId; public Guid UserId { get => userId; set => Set(ref userId, value); }
        private int level; public int Level { get => level; set => Set(ref level, value); }
        private double experience; public double Experience { get => experience; set => Set(ref experience, value); }
        private string name; public string Name { get => name; set => Set(ref name, value); }
        private string logo; public string Logo { get => logo; set => Set(ref logo, value); }
        private int nameChangeCount; public int NameChangeCount { get => nameChangeCount; set => Set(ref nameChangeCount, value); }
        private bool canChangeName; public bool CanChangeName { get => canChangeName; set => Set(ref canChangeName, value); }
        private DateTime created; public DateTime Created { get => created; set => Set(ref created, value); }
    }
}
