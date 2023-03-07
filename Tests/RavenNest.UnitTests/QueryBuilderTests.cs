using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;

namespace RavenNest.UnitTests
{
    [TestClass]
    public class QueryBuilderTests
    {

        [TestMethod]
        public void TestQueryBuild()
        {
            GameDataMigration migration = new GameDataMigration(null);
            GameDataBackupProvider backupProvider = new GameDataBackupProvider(null);

            IEntityRestorePoint restorePoint = backupProvider.GetRestorePoint(new[] {
                        typeof(Appearance),
                        typeof(SyntyAppearance),
                        typeof(Character),
                        typeof(CharacterState),
                        typeof(InventoryItem),
                        typeof(User),
                        typeof(GameSession),
                        typeof(Village),
                        typeof(VillageHouse),
                        typeof(Resources),
                        typeof(Skills),
                        typeof(Statistics),
                        typeof(MarketItem)});

            migration.MigrateTest(restorePoint);

        }


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
