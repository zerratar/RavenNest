using System.Collections.Generic;

namespace RavenNest.Models
{
    public class ResourceItemDropCollection : Collection<ResourceItemDrop>
    {
        public ResourceItemDropCollection()
        {
        }

        public ResourceItemDropCollection(IEnumerable<ResourceItemDrop> items)
            : base(items)
        {
        }
    }
}
