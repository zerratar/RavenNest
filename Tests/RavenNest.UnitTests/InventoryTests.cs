using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.BusinessLogic.Providers;

namespace RavenNest.UnitTests
{
    [TestClass]
    public class InventoryTests
    {
        private void AddItems(PlayerInventory inventory, Guid itemId, int totalItemAmount, int tickTimeMs)
        {
            for (var i = 0; i < totalItemAmount; ++i)
            {
                inventory.AddItem(itemId);
                System.Threading.Thread.Sleep(tickTimeMs);
            }
        }
    }
}
