using System;

namespace RavenNest.Models
{
    /// <summary>
    ///     A character's skill trees as the server has them (combat v2, the game's docs/combat-skills-design.md): the build
    ///     in RavenNest.Combat.SkillBuild's text form, the points earned and spent, and what a respec costs.
    /// </summary>
    public class SkillBuildInfo
    {
        public Guid CharacterId { get; set; }
        public string Build { get; set; }
        public int Earned { get; set; }
        public int Spent { get; set; }
        public int FreeRespecs { get; set; }
        public int RespecCost { get; set; }
        public int TomesOfForgetting { get; set; }
    }

    /// <summary>The answer to a skill tree change: done, or why not, with the build as it is now.</summary>
    public class SkillBuildResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public SkillBuildInfo Info { get; set; }
    }
}
