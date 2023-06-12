using RavenNest.Models;

namespace RavenNest.BusinessLogic.Models
{
    public class TwitchExtensionRequestContext
    {
        public DataModels.User Principal { get; set; }
        public DataModels.Character Character { get; set; }
        public DataModels.GameSession GameSession { get; set; }
        public SessionInfo SessionInfo { get; set; }
    }
}
