using System.Collections;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public class MarketItemCollection : IList<MarketItem>
    {
        private readonly List<MarketItem> items = new List<MarketItem>();

        public int Offset { get; set; }
        public int Total { get; set; }

        public IEnumerator<MarketItem> GetEnumerator()
        {
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(MarketItem item)
        {
            this.items.Add(item);
        }

        public void Clear()
        {
            this.items.Clear();
        }

        public bool Contains(MarketItem item)
        {
            return this.items.Contains(item);
        }

        public void CopyTo(MarketItem[] array, int arrayIndex)
        {
            this.items.CopyTo(array, arrayIndex);
        }

        public bool Remove(MarketItem item)
        {
            return this.items.Remove(item);
        }

        public int Count => this.items.Count;

        public bool IsReadOnly => false;

        public void AddRange(IEnumerable<MarketItem> items)
        {
            foreach (var item in items) this.Add(item);
        }

        public int IndexOf(MarketItem item)
        {
            return this.items.IndexOf(item);
        }

        public void Insert(int index, MarketItem item)
        {
            this.items.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            this.items.RemoveAt(index);
        }

        public MarketItem this[int index]
        {
            get => this.items[index];
            set => this.items[index] = value;
        }
    }
}