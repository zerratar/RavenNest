using System.Collections.Generic;

namespace RavenNest.Models
{
    public class BeginSessionResult
    {
        public SessionToken SessionToken { get; set; }
        public ExpMultiplier ExpMultiplier { get; set; }
        public VillageInfo Village { get; set; }
        public SessionSettings Permissions { get; set; }
        public string ExpectedClientVersion { get; set; }
        public BeginSessionResultState State { get; set; }
        public Dictionary<string, object> UserSettings { get; set; }

        /// <summary>
        /// The signed token for the v2 game connection (Hello). Games before v2 ignore it.
        /// </summary>
        public string ConnectionToken { get; set; }
        public static BeginSessionResult InvalidVersion { get; set; } = new BeginSessionResult
        {
            State = BeginSessionResultState.UpdateRequired
        };

        public static BeginSessionResult UserDoesNotExist { get; set; } = new BeginSessionResult
        {
            State = BeginSessionResultState.UnknownError
        };

    }
}
