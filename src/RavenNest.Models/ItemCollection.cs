using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace RavenNest.Models
{
    public class ItemCollection : IList<Item>
    {
        private readonly List<Item> items = new List<Item>();

        public ItemCollection()
        {
        }

        public ItemCollection(IEnumerable<Item> items)
        {
            this.items = items.ToList();
        }

        public IEnumerator<Item> GetEnumerator()
        {
            return items.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(Item item)
        {
            this.items.Add(item);
        }

        public void Clear()
        {
            this.items.Clear();
        }

        public bool Contains(Item item)
        {
            return this.items.Contains(item);
        }

        public void CopyTo(Item[] array, int arrayIndex)
        {
            this.items.CopyTo(array, arrayIndex);
        }

        public bool Remove(Item item)
        {
            return this.items.Remove(item);
        }

        public int Count => this.items.Count;

        public bool IsReadOnly => false;

        public int IndexOf(Item item)
        {
            return this.items.IndexOf(item);
        }

        public void Insert(int index, Item item)
        {
            this.items.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            this.items.RemoveAt(index);
        }

        public Item this[int index]
        {
            get => this.items[index];
            set => this.items[index] = value;
        }
    }
}
