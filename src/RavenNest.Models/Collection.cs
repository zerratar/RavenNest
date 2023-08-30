using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Models
{
    public class Collection<T> : IList<T>
    {
        private readonly List<T> items = new List<T>();

        public Collection()
        {
        }

        public Collection(IEnumerable<T> items)
        {
            this.items = items.ToList();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(T item)
        {
            this.items.Add(item);
        }

        public void Clear()
        {
            this.items.Clear();
        }

        public bool Contains(T item)
        {
            return this.items.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            this.items.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            return this.items.Remove(item);
        }

        public int Count => this.items.Count;

        public bool IsReadOnly => false;

        public int IndexOf(T item)
        {
            return this.items.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            this.items.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            this.items.RemoveAt(index);
        }

        public T this[int index]
        {
            get => this.items[index];
            set => this.items[index] = value;
        }
    }
}
