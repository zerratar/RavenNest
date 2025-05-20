using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class GameClient : Entity<GameClient>
    {
        [PersistentData] private string clientVersion;
        [PersistentData] private string accessKey;
        [PersistentData] private string downloadLink;
    }
}
