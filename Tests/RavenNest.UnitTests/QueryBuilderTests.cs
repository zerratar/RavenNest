using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.DataModels;

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
            threadA.Start();
            threadB.Start();
            threadC.Start();
            threadA.Join();
            threadB.Join();
            threadC.Join();
            var stack = inventory.GetItem(itemId, false);
            Assert.AreEqual(110, stack.Amount);
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

    [TestClass]
    public class HashTest
    {
        [TestMethod]
        public void GenerateHash1()
        {
            var hasher = new SecureHasher();
        }
    }

    [TestClass]
    public class QueryBuilderTests
    {

        [TestMethod]
        public void Test_insert()
        {
            var qb = new QueryBuilder();
            List<DataModels.IEntity> entities = new List<DataModels.IEntity>();

            for (var i = 0; i < 10; ++i)
            {
                entities.Add(new DataModels.User()
                {
                    Created = DateTime.UtcNow,
                    Id = Guid.Empty,
                    Email = "asdasda@asdasdasd.com",
                    DisplayName = "displayName" + i,
                    IsAdmin = true,
                    IsModerator = false,
                    PasswordHash = "123123123123",
                    UserId = i.ToString("00000"),
                    UserName = "userName" + i
                });
            }

            var data = new EntityStoreItems(DataModels.EntityState.Added, entities);
            var result = qb.Build(data);

        }

        [TestMethod]
        public void Test_modified()
        {
            var qb = new QueryBuilder();
            List<DataModels.IEntity> entities = new List<DataModels.IEntity>();

            for (var i = 0; i < 10; ++i)
            {
                entities.Add(new DataModels.User()
                {
                    Created = DateTime.UtcNow,
                    Id = Guid.Empty,
                    Email = "asdasda@asdasdasd.com",
                    DisplayName = "displayName" + i,
                    IsAdmin = true,
                    IsModerator = false,
                    PasswordHash = "123123123123",
                    UserId = i.ToString("00000"),
                    UserName = "userName" + i
                });
            }

            var data = new EntityStoreItems(DataModels.EntityState.Modified, entities);
            var result = qb.Build(data);
        }

        [TestMethod]
        public void Test_delete()
        {
            var qb = new QueryBuilder();
            List<DataModels.IEntity> entities = new List<DataModels.IEntity>();

            for (var i = 0; i < 10; ++i)
            {
                entities.Add(new DataModels.User()
                {
                    Created = DateTime.UtcNow,
                    Id = Guid.Empty,
                    Email = "asdasda@asdasdasd.com",
                    DisplayName = "displayName" + i,
                    IsAdmin = true,
                    IsModerator = false,
                    PasswordHash = "123123123123",
                    UserId = i.ToString("00000"),
                    UserName = "userName" + i
                });
            }

            var data = new EntityStoreItems(DataModels.EntityState.Deleted, entities);
            var result = qb.Build(data);
        }
    }
}
