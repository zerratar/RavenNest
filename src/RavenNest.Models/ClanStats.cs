using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public class ClanStats
    {
        public string Name { get; set; }
        public string OwnerName { get; set; }
        public int Level { get; set; }
        public float ProgressToLevel { get; set; }
        public List<ClanSkillInfo> ClanSkills { get; set; }
    }

    public class ClanSkillInfo
    {
        public string Name { get; set; }
        public int Level { get; set; }
    }
}
