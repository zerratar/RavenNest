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
        private bool isPublic; public bool IsPublic { get => isPublic; set => Set(ref isPublic, value); }
    }

    public partial class ClanRolePermissions : Entity<ClanRolePermissions>
    {
        private Guid clanRoleId; public Guid ClanRoleId { get => clanRoleId; set => Set(ref clanRoleId, value); }
        private string permissions; public string Permissions { get => permissions; set => Set(ref permissions, value); }
    }

    public partial class CharacterClanSkillCooldown : Entity<CharacterClanSkillCooldown>
    {
        private Guid characterId; public Guid CharacterId { get => characterId; set => Set(ref characterId, value); }
        private Guid skillId; public Guid SkillId { get => skillId; set => Set(ref skillId, value); }
        private DateTime cooldownStart; public DateTime CooldownStart { get => cooldownStart; set => Set(ref cooldownStart, value); }
        private DateTime cooldownEnd; public DateTime CooldownEnd { get => cooldownEnd; set => Set(ref cooldownEnd, value); }
    }
}
