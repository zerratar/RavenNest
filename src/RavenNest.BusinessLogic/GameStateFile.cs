using System.Collections.Generic;

namespace RavenNest.BusinessLogic
{
    public class GameStateFile
    {
        public Net.DeltaTcpLib.GameStateRequest GameState { get; set; }
        public List<Net.DeltaTcpLib.DeltaExperienceUpdate> ExperienceState { get; set; }
        public List<Net.DeltaTcpLib.CharacterStateDelta> PlayerState { get; set; }
    }
}
