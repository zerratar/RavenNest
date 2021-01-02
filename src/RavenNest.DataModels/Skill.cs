using System;

namespace RavenNest.DataModels
{
    public partial class Skill : Entity<Skill>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private string name; public string Name { get => name; set => Set(ref name, value); }
        private int maxLevel; public int MaxLevel { get => maxLevel; set => Set(ref maxLevel, value); }
        private int requiredClanLevel; public int RequiredClanLevel { get => requiredClanLevel; set => Set(ref requiredClanLevel, value); }
        private int type; public int Type { get => type; set => Set(ref type, value); }
    }
}
