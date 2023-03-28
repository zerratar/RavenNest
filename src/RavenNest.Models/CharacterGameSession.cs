using System;

namespace RavenNest
{
    public class CharacterGameSession
    {
        public Guid CharacterId { get; set; }
        public string CharacterName { get; set; }
        public int CharacterIndex { get; set; }
        public int CharacterCombatLevel { get; set; }
        public Guid SessionOwnerUserId { get; set; }
        public string SessionOwnerUserName { get; set; }

        [Obsolete]
        public string SessionTwitchUserId { get; set; }
        public DateTime Joined { get; set; }
    }
}
