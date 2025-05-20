using RavenNest.DataAnnotations;

namespace RavenNest.DataModels
{
    public partial class Achievement : Entity<Achievement>
    {
        [PersistentData] private string name;
        [PersistentData] private string description;
    }
}
