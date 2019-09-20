using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RavenNest
{
    public class ItemRepository : JsonBasedRepository<CraftableItemDefinition>
    {
        public ItemRepository(string repositoryFolder)
            : base(System.IO.Path.Combine(repositoryFolder, "items"))
        {
        }

        protected override string GetKey(CraftableItemDefinition item)
        {
            return GetKeyImp(item);
        }

        public IReadOnlyList<CraftableItemDefinition> All()
        {
            return this.items.Values.ToList();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetKeyImp(CraftableItemDefinition item)
        {
            return item.Item.Id.ToString();
        }

        public IReadOnlyDictionary<string, CraftableItemDefinition> Clear()
        {
            var clone = this.items.ToDictionary(x => x.Key, x => x.Value);
            this.items.Clear();
            return clone;
        }

        public void LoadFrom(IReadOnlyDictionary<string, CraftableItemDefinition> oldItems)
        {
            foreach (var i in oldItems)
                this.items[i.Key] = i.Value;
        }
    }
}
