using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     A character's skill tree choices (combat v2, the game's docs/combat-skills-design.md): ranks in the nodes and
    ///     the loadout, in RavenNest.Combat.SkillBuild's text form ("cleave:1;brute-force:3|cleave,taunt"). Made the first
    ///     time the character learns something; the table is made at startup.
    /// </summary>
    public partial class CharacterSkillBuild : Entity<CharacterSkillBuild>
    {
        [PersistentData] private Guid characterId;
        [PersistentData] private string build;

        /// <summary>Respecs that cost nothing: one when the trees arrive, one after each balance update that changes them.</summary>
        [PersistentData] private int freeRespecs;

        /// <summary>RavenNest.Combat.SkillTrees.Version when the build was last looked at: an older one earns a free respec.</summary>
        [PersistentData] private int treeVersion;

        [PersistentData] private DateTime? updated;
    }
}
