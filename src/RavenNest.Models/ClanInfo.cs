using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public class ClanInfo
    {
        public string Name { get; set; }
        public string OwnerName { get; set; }
        public List<ClanRoleInfo> Roles { get; set; }
    }

    public class ClanRoleInfo
    {
        public string Name { get; set; }
        public int MemberCount { get; set; }
        public int Level { get; set; }
    }
}
