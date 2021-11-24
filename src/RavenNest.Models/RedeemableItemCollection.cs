using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Models
{
    public class RedeemableItemCollection : IList<RedeemableItem>
    {
        private readonly List<RedeemableItem> items = new List<RedeemableItem>();

        public RedeemableItemCollection()
        {
        }

        public RedeemableItemCollection(IEnumerable<RedeemableItem> items)
        {
            this.items = items.ToList();
        }

        public IEnumerator<RedeemableItem> GetEnumerator()
        {
            return items.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(RedeemableItem item)
        {
            this.items.Add(item);
        }

        public void Clear()
        {
            this.items.Clear();
        }

        public bool Contains(RedeemableItem item)
        {
            return this.items.Contains(item);
        }

        public void CopyTo(RedeemableItem[] array, int arrayIndex)
        {
            this.items.CopyTo(array, arrayIndex);
        }

        public bool Remove(RedeemableItem item)
        {
            return this.items.Remove(item);
        }

        public int Count => this.items.Count;

        public bool IsReadOnly => false;

        public int IndexOf(RedeemableItem item)
        {
            return this.items.IndexOf(item);
        }

        public void Insert(int index, RedeemableItem item)
        {
            this.items.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            this.items.RemoveAt(index);
        }

        public RedeemableItem this[int index]
        {
            get => this.items[index];
            set => this.items[index] = value;
        }
    }
}
