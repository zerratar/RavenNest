using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Models
{
    public class EventList
    {
        public List<GameEvent> Events { get; set; }
        public int Revision { get; set; }
    }

    public class EventCollection : IList<GameEvent>
    {
        private readonly List<GameEvent> events;

        public int Revision { get; set; }

        public EventCollection()
        {
            this.events = new List<GameEvent>();
        }

        public EventCollection(IEnumerable<GameEvent> events)
        {
            this.events = events.ToList();
        }

        public IEnumerator<GameEvent> GetEnumerator()
        {
            return events.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(GameEvent item)
        {
            events.Add(item);
        }

        public void Clear()
        {
            events.Clear();
        }

        public bool Contains(GameEvent item)
        {
            return events.Contains(item);
        }

        public void CopyTo(GameEvent[] array, int arrayIndex)
        {
            events.CopyTo(array, arrayIndex);
        }

        public bool Remove(GameEvent item)
        {
            return events.Remove(item);
        }

        public int Count => events.Count;

        public bool IsReadOnly => false;

        public int IndexOf(GameEvent item)
        {
            return events.IndexOf(item);
        }

        public void Insert(int index, GameEvent item)
        {
            events.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            events.RemoveAt(index);
        }

        public GameEvent this[int index]
        {
            get => events[index];
            set => events[index] = value;
        }
    }
}