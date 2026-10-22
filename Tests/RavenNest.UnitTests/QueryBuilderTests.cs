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

        private static List<IEntity> Users(int count, string name)
        {
            var entities = new List<IEntity>();
            for (var i = 0; i < count; ++i)
            {
                entities.Add(new User
                {
                    Created = DateTime.UtcNow.AddMinutes(-i),
                    Id = Guid.NewGuid(),
                    Email = name + i + "@example.com",
                    DisplayName = name + "'s display " + i,
                    UserId = i.ToString("00000"),
                    UserName = name + i
                });
            }
            return entities;
        }

        [TestMethod]
        public void Saves_are_parameterized_with_the_same_text_for_the_same_shape()
        {
            var qb = new QueryBuilder();
            foreach (var state in new[] { EntityState.Added, EntityState.Modified, EntityState.Deleted })
            {
                var a = qb.Build(new EntityStoreItems(state, Users(10, "alice")));
                var b = qb.Build(new EntityStoreItems(state, Users(10, "bob")));
                Assert.AreEqual(a.Commands.Count, b.Commands.Count, state.ToString());
                for (var i = 0; i < a.Commands.Count; ++i)
                {
                    // the same text whatever the values, so SQL Server reuses one plan
                    Assert.AreEqual(a.Commands[i].Text, b.Commands[i].Text, state.ToString());
                    Assert.IsFalse(a.Commands[i].Text.Contains("alice"), state + ": a value in the SQL text");
                }
                // the old SQL with the values is still there, for the logs
                StringAssert.Contains(a.Command, state == EntityState.Deleted ? "WHERE" : "alice");
            }
        }

        [TestMethod]
        public void Nulls_keep_the_parameter_types_of_their_properties()
        {
            // A character's UserIdLock, TitleId and LastUsed are often null. If a null changed a parameter's type,
            // every mix of nulls in a command would be a new statement and a new plan.
            List<IEntity> Characters(bool withNulls)
            {
                var entities = new List<IEntity>();
                for (var i = 0; i < 10; ++i)
                {
                    var useNull = withNulls && i % 2 == 0;
                    entities.Add(new Character
                    {
                        Id = Guid.NewGuid(),
                        Name = "character" + i,
                        UserIdLock = useNull ? null : Guid.NewGuid(),
                        TitleId = useNull ? null : Guid.NewGuid(),
                        LastUsed = useNull ? null : DateTime.UtcNow
                    });
                }
                return entities;
            }

            var qb = new QueryBuilder();
            foreach (var state in new[] { EntityState.Added, EntityState.Modified })
            {
                var all = qb.Build(new EntityStoreItems(state, Characters(false)));
                var mixed = qb.Build(new EntityStoreItems(state, Characters(true)));
                Assert.AreEqual(all.Commands.Count, mixed.Commands.Count, state.ToString());
                for (var c = 0; c < all.Commands.Count; ++c)
                {
                    Assert.AreEqual(all.Commands[c].Text, mixed.Commands[c].Text, state.ToString());
                    var a = all.Commands[c].Parameters;
                    var b = mixed.Commands[c].Parameters;
                    Assert.AreEqual(a.Count, b.Count, state.ToString());
                    for (var i = 0; i < a.Count; ++i)
                    {
                        Assert.AreEqual(a[i].SqlDbType, b[i].SqlDbType, state + " " + a[i].ParameterName);
                        Assert.AreEqual(a[i].Size, b[i].Size, state + " " + a[i].ParameterName);
                    }
                }
            }
        }

        [TestMethod]
        public void Saves_of_any_size_use_only_a_few_statement_shapes()
        {
            // every distinct command text is a plan in SQL Server: saves of 1 to 300 rows must not make 300 of them
            var qb = new QueryBuilder();
            foreach (var state in new[] { EntityState.Added, EntityState.Modified, EntityState.Deleted })
            {
                var texts = new HashSet<string>();
                for (var rows = 1; rows <= 300; ++rows)
                {
                    foreach (var command in qb.Build(new EntityStoreItems(state, Users(rows, "dave"))).Commands)
                    {
                        texts.Add(command.Text);
                    }
                }
                Assert.IsTrue(texts.Count <= 16, state + ": " + texts.Count + " different statements");
            }
        }

        [TestMethod]
        public void Large_saves_stay_under_the_parameter_limit()
        {
            var qb = new QueryBuilder();
            foreach (var state in new[] { EntityState.Added, EntityState.Modified, EntityState.Deleted })
            {
                var query = qb.Build(new EntityStoreItems(state, Users(2500, "carol")));
                Assert.IsTrue(query.Commands.Count > 1, state + ": one command for 2500 rows");
                foreach (var command in query.Commands)
                {
                    Assert.IsTrue(command.Parameters.Count <= 2000, state + ": " + command.Parameters.Count + " parameters");
                }
            }
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
