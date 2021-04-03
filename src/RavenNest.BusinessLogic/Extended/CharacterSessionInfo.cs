using System;

namespace RavenNest.BusinessLogic.Extended
{
    public class CharacterSessionInfo
    {
        public DateTime Started { get; internal set; }
        public string OwnerDisplayName { get; internal set; }
        public string OwnerUserName { get; internal set; }
    }
}
