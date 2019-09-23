namespace RavenNest.BusinessLogic.Net
{
    public class CharacterStateUpdate
    {
        public CharacterStateUpdate(
            string userId,
            int health,
            string island,
            string duelOpponent,
            bool inRaid,
            bool inArena,
            string task,
            string taskArgument,
            Vector3 position)
        {
            UserId = userId;
            Health = health;
            Island = island;
            DuelOpponent = duelOpponent;
            InRaid = inRaid;
            InArena = inArena;
            Task = task;
            TaskArgument = taskArgument;
            Position = position;
        }
        public string UserId { get; }
        public int Health { get; }
        public string Island { get; }
        public string DuelOpponent { get; }
        public bool InRaid { get; }
        public bool InArena { get; }
        public string Task { get; }
        public string TaskArgument { get; }
        public Vector3 Position { get; }
    }
}