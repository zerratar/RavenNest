using System;

namespace RavenNest.DataModels
{
    public partial class ClanRole : Entity<ClanRole>
    {

        private Guid clanId; public Guid ClanId { get => clanId; set => Set(ref clanId, value); }
        private int cape; public int Cape { get => cape; set => Set(ref cape, value); }
        private int level; public int Level { get => level; set => Set(ref level, value); }
        private string name; public string Name { get => name; set => Set(ref name, value); }
    }
}
