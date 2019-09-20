using System;

namespace RavenNest.DataModels
{
    public partial class GameClient
    {
        public Guid Id { get; set; }
        public string ClientVersion { get; set; }
        public string AccessKey { get; set; }
    }
}