using System.Collections.Generic;

namespace RavenNest
{
    public class PlayerRepository : JsonBasedRepository<PlayerDefinition>
    {
        public PlayerRepository(string folder)
            : base(System.IO.Path.Combine(folder, "Players\\"))
        {
        }

        protected override string GetKey(PlayerDefinition item)
        {
            return item.Name;
        }

        public void UpdateMany(IEnumerable<PlayerDefinition> players)
        {
            foreach (var player in players)
            {
                var key = GetKey(player);
                this.items[key] = player;
            }
        }
    }
}
