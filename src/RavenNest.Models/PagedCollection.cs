using System.Collections.Generic;

namespace RavenNest.Models
{
    public class PagedCollection<T>
    {
        public long TotalSize { get; set; }
        public IReadOnlyList<T> Items { get; set; }
        public PagedCollection() { }
        public PagedCollection(IReadOnlyList<T> source, int totalSize)
        {
            TotalSize = totalSize;
            Items = source;
        }
    }
}
