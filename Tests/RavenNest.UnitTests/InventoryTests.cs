using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RavenNest.UnitTests
{
    [TestClass]
    public class InventoryTests
    {

        [TestMethod]
        public void TestInventory()
        {
            var provider = new PlayerInventoryProvider(null);
            var playerId = Guid.NewGuid();
            var inventory = provider.Get(playerId);
            var itemId = Guid.Empty;
            var threadA = new Thread(new ThreadStart(() => AddItems(inventory, itemId, 10, 500)));
            var threadB = new Thread(new ThreadStart(() => AddItems(inventory, itemId, 50, 100)));
            var threadC = new Thread(new ThreadStart(() => AddItems(inventory, itemId, 50, 1)));
            var threadD = new Thread(new ThreadStart(() => EquipItem(inventory, itemId)));
            var threadE = new Thread(new ThreadStart(() => UnequipItem(inventory, itemId)));
            threadA.Start();
            threadB.Start();
            threadC.Start();
            threadD.Start();
            threadE.Start();

            threadC.Join();
            threadD.Join();
            threadE.Join();
            threadA.Join();
            threadB.Join();

            var stack = inventory.GetItem(itemId, false);
            Assert.AreEqual(110, stack.Amount);
        }
        private void EquipItem(PlayerInventory inventory, Guid itemId)
        {
            for (var i = 0; i < 100; ++i)
            {
                inventory.EquipItem(itemId);
                System.Threading.Thread.Sleep(1);
            }
        }

        private void UnequipItem(PlayerInventory inventory, Guid itemId)
        {
            for (var i = 0; i < 100; ++i)
            {
                inventory.UnequipItem(itemId);
                System.Threading.Thread.Sleep(1);
            }
        }

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
