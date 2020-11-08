using System.Collections.Generic;

namespace RavenNest
{
    public class InventoryDefinition
    {
        public InventoryDefinition(List<ItemDefinition> backpack, List<ItemDefinition> equipped)
        {
            Backpack = backpack ?? new List<ItemDefinition>();
            Equipped = equipped ?? new List<ItemDefinition>();
        }

        public List<ItemDefinition> Backpack { get; }
        public List<ItemDefinition> Equipped { get; }
    }
}
