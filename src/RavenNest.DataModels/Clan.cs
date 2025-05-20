using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class Clan : Entity<Clan>
    {
        [PersistentData] private Guid userId;
        [PersistentData] private int level;
        [PersistentData] private double experience;
        [PersistentData] private string name;
        [PersistentData] private string logo;
        [PersistentData] private int nameChangeCount;
        [PersistentData] private bool canChangeName;
        [PersistentData] private DateTime created;
        [PersistentData] private bool isPublic;
    }

    public partial class ClanRolePermissions : Entity<ClanRolePermissions>
    {
        [PersistentData] private Guid clanRoleId;
        [PersistentData] private string permissions;
    }

    public partial class CharacterClanSkillCooldown : Entity<CharacterClanSkillCooldown>
    {
        [PersistentData] private Guid characterId;
        [PersistentData] private Guid skillId;
        [PersistentData] private DateTime cooldownStart;
        [PersistentData] private DateTime cooldownEnd;
    }
}
