using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.DataModels;

namespace RavenNest.UnitTests
{
    [TestClass]
    public class ExtendedSkillsTest
    {
        [TestMethod]
        public void TestStrangeProcent()
        {
            decimal exp = 995303420;
            int level = GameMath.ExperienceToLevel(exp);
            decimal thisLevel = GameMath.LevelToExperience(level);
            decimal nextLevel = GameMath.LevelToExperience(level + 1);
            decimal deltaExp = exp - thisLevel;
            decimal deltaNextLevel = nextLevel - thisLevel;
            float procent = (float)(deltaExp / deltaNextLevel);

        }
    }

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
