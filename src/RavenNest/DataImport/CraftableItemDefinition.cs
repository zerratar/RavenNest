namespace RavenNest
{
    public class CraftableItemDefinition
    {
        public CraftableItemDefinition(ItemDefinition item, CraftingRequirements craftingRequirements)
        {
            Item = item;
            CraftingRequirements = craftingRequirements;
        }

        public ItemDefinition Item { get; }
        public CraftingRequirements CraftingRequirements { get; }
    }
}
