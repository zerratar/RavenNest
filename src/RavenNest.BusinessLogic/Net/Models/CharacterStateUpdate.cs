namespace RavenNest.BusinessLogic.Net
{
    public class CharacterStateUpdate
    {
        public string UserId { get; set; }
        public int Health { get; set; }
        public string Island { get; set; }
        public string DuelOpponent { get; set; }
        public bool InRaid { get; set; }
        public bool InArena { get; set; }
        public string Task { get; set; }
        public string TaskArgument { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public class CharacterSkillUpdate
    {
        public string UserId { get; set; }
        public decimal[] Experience { get; set; }
    }

}