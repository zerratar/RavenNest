using System.Collections.Generic;

namespace RavenNest.Models
{
    public class ItemCollection : Collection<Item>
    {
        public ItemCollection()
        {
        }

        public ItemCollection(IEnumerable<Item> items) : base(items)
        {
        }
    }
}
