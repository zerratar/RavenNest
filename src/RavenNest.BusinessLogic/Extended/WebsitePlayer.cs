using RavenNest.Models;
using System;

namespace RavenNest.BusinessLogic.Extended
{
    public class WebsitePlayer : Player
    {
        public new SkillsExtended Skills { get; set; }

        public CharacterSessionInfo SessionInfo { get; set; }
    }

    public class CharacterSessionInfo
    {
        public DateTime Started { get; internal set; }
        public string OwnerDisplayName { get; internal set; }
        public string OwnerUserName { get; internal set; }
    }
}
