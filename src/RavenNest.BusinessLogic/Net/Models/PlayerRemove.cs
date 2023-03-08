using System;

namespace RavenNest.BusinessLogic.Net
{
    public class PlayerRemove
    {
        public string Reason { get; set; }
        public Guid UserId { get; set; }
        public Guid CharacterId { get; set; }
    }
}
