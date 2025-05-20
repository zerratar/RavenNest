using RavenNest.DataAnnotations;

namespace RavenNest.DataModels
{
    public partial class Skill : Entity<Skill>
    {
        [PersistentData] private string name;
        [PersistentData] private int maxLevel;
        [PersistentData] private int requiredClanLevel;
        [PersistentData] private int type;
    }
}
