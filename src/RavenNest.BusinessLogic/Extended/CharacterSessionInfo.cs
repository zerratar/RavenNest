using RavenNest.Models;
using System;

namespace RavenNest.BusinessLogic.Extended
{
    public class CharacterSessionInfo
    {
        public DateTime Started { get; internal set; }
        public DateTime SkillsUpdated { get; set; }
        public string OwnerDisplayName { get; set; }
        public string OwnerUserName { get; set; }
    }

}
