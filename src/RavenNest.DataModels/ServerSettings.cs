using RavenNest.DataAnnotations;

namespace RavenNest.DataModels
{
    public partial class ServerSettings : Entity<ServerSettings>
    {
        [PersistentData] private string name;
        [PersistentData] private string value;
    }
}
