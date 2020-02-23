using System.Collections.Generic;

namespace RavenNest.Models
{
    public class PagedPlayerCollection
    {
        public long TotalSize { get; set; }
        public IReadOnlyList<Player> Players { get; set; }
    }
}