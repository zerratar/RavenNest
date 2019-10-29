using System.Threading.Tasks;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Game;
using Item = RavenNest.Models.Item;

namespace RavenNest
{
    public class ItemImporter
    {
        private readonly IItemManager itemManager;
        public ItemImporter(IItemManager itemManager)
        {
            this.itemManager = itemManager;
        }

        //public async Task<string> ImportJsonDatabaseAsync()
        //{
        //    var itemRepo = new ItemRepository("E:\\git\\Ravenfall\\Data\\Repositories");
        //    try
        //    {
        //        foreach (var item in itemRepo.All())
        //        {
        //            var i = DataMapper.Map<Item, ItemDefinition>(item.Item);
        //            i.Craftable = i.RequiredCraftingLevel >= 1 && i.RequiredCraftingLevel <= 170;
        //            i.RequiredCraftingLevel = item.CraftingRequirements.MinCraftingLevel;
        //            i.OreCost = item.CraftingRequirements.Ore;
        //            i.WoodCost = item.CraftingRequirements.Wood;

        //            await itemManager.AddItemAsync(null, i);
        //        }

        //        return "yes";
        //    }
        //    catch
        //    {
        //        return "no";
        //    }
        //}
    }
}
