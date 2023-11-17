using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;
using System.Collections;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Providers
{
    public class InventoryItemCollection : IList<InventoryItem>
    {
        private readonly GameData gameData;
        private readonly List<InventoryItem> items;
        public AddEntityResult LastAddResult { get; set; }
        public RemoveEntityResult LastRemoveResult { get; set; }
        public InventoryItemCollection(GameData gameData, List<InventoryItem> items)
        {
            this.gameData = gameData;
            this.items = items;
        }

        public InventoryItem this[int index] { get => items[index]; set => items[index] = value; }

        public int Count => items.Count;

        public bool IsReadOnly => false;

        public void Add(InventoryItem item)
        {
            items.Add(item);
            LastAddResult = gameData.Add(item);
        }

        public void Clear()
        {
            foreach (var item in items)
            {
                gameData.Remove(item);
            }

            items.Clear();
        }

        public bool Contains(InventoryItem item)
        {
            return items.Contains(item); // do we want to check gameData too?
        }

        public void CopyTo(InventoryItem[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<InventoryItem> GetEnumerator()
        {
            return items.GetEnumerator();
        }

        public int IndexOf(InventoryItem item)
        {
            return items.IndexOf(item);
        }

        public void Insert(int index, InventoryItem item)
        {
            throw new NotImplementedException();
        }

        public bool Remove(InventoryItem item)
        {
            var res = items.Remove(item);
            LastRemoveResult = gameData.Remove(item);
            return res && LastRemoveResult == RemoveEntityResult.Success;
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
