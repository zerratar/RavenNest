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
        /// <summary>The cloth of the clan flag and clan sails on members' boats (#RRGGBB), or null for the plain colours.</summary>
        [PersistentData] private string flagColor;
        [PersistentData] private string sailColor;
        [PersistentData] private int nameChangeCount;
        [PersistentData] private bool canChangeName;
        [PersistentData] private DateTime created;
        [PersistentData] private bool isPublic;

        /// <summary>
        /// Who may join (RavenNest.Models.ClanJoinPolicy): 0 anyone, with !clan join or the clan page (what every clan
        /// was before join policies existed, and still the default); 1 on request, decided by ranks that may invite;
        /// 2 by invite only. isPublic above was never set anywhere and is no longer read.
        /// </summary>
        [PersistentData] private int joinPolicy;

        // The clan's own images (ClanImage ids), empty for the defaults: the owner's Twitch picture, and that
        // logo on a cloth colour for the cape. The shape of the cape: 0 long, 1 pointed, 2 short, 3 tattered.
        [PersistentData] private Guid? logoImageId;
        [PersistentData] private Guid? capeImageId;
        [PersistentData] private int capeStyle;

        /// <summary>The clan page's banner (a ClanImage of kind Banner), or null for none.</summary>
        [PersistentData] private Guid? bannerImageId;
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
