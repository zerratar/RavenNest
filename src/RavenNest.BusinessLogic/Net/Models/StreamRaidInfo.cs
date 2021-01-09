using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Net
{
    public class StreamRaidInfo
    {
        public string RaiderUserName { get; set; }
        public string RaiderUserId { get; set; }
        public List<UserCharacter> Players { get; set; }
    }
}
