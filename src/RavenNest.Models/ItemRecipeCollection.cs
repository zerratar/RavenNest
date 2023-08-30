using System.Collections.Generic;

namespace RavenNest.Models
{
    public class ItemRecipeCollection : Collection<ItemRecipe>
    {
        public ItemRecipeCollection()
        {
        }

        public ItemRecipeCollection(IEnumerable<ItemRecipe> items)
            : base(items)
        {
        }
    }
}
