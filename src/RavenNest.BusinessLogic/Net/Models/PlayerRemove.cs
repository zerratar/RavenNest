using System;

namespace RavenNest.BusinessLogic.Net
{
    public class PlayerRemove
    {
        public string UserId { get; set; }
        public string Reason { get; set; }
        public Guid CharacterId { get; set; }
    }
}
