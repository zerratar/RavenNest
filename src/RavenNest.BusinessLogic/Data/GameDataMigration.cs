﻿using Microsoft.Extensions.Logging;
using RavenNest.DataModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace RavenNest.BusinessLogic.Data
{
    public class DataMigrationException : Exception
    {
        public DataMigrationException(string message)
            : base(message)
        {
        }
    }

    public class GameDataMigration
    {
        private readonly ILogger<GameDataMigration> logger;
        private readonly HashSet<Guid> importedUsers = new HashSet<Guid>();
        public GameDataMigration(ILogger<GameDataMigration> logger)
        {
            this.logger = logger;
        }

        public bool TryMigrate(
            IRavenfallDbContextProvider db,
            IEntityRestorePoint restorePoint,
            out List<Type> migratedTypes,
            out List<Type> failedTypes,
            out Exception failedException)
        {
            migratedTypes = new List<Type>();
            failedTypes = new List<Type>();
            failedException = null;
            try
            {
                logger.LogWarning("Restoring data from restorepoint..");
                var sw = new Stopwatch();
                sw.Start();
                var queryBuilder = new QueryBuilder();
                using (var con = db.GetConnection())
                {
                    con.Open();

                    logger.LogInformation("Truncating db...");

                    var restoreTypes = restorePoint.GetEntityTypes();
                    foreach (var restoreType in restoreTypes)
                    {
                        if (restoreType == typeof(ItemCraftingRequirement))
                            continue;
                        var dbName = db.GetDbName();
                        var table = $"[{dbName}].[dbo].[{restoreType.Name}]";

                        var entities = restorePoint.Get(restoreType);
                        if (entities == null)
                        {
                            continue;
                        }

                        logger.LogWarning("Restoring " + restoreType.Name + " with " + entities.Count + " records.");

                        if (TryTruncateTable(con, table))
                        {
                            logger.LogInformation($"Migrating {table} data...");
                            try
                            {

                                var queries = BuildInsertQuery(queryBuilder, entities);
                                foreach (var q in queries)
                                {
                                    using (var cmd = con.CreateCommand())
                                    {
                                        cmd.CommandText = q;
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                                migratedTypes.Add(restoreType);
                            }
                            catch (Exception exc)
                            {
                                failedTypes.Add(restoreType);
                                failedException = exc;
                                TryTruncateTable(con, table); // truncate it again to leave it empty for quicker restore next time.
                                logger.LogError($"Failed to migrate data in {table}. Table skipped and restorepoint kept. Exception: " + exc);
                            }
                        }
                    }
                    con.Close();
                }
                var typeCount = migratedTypes.Count + failedTypes.Count;
                logger.LogWarning(migratedTypes.Count + " / " + typeCount + " tables was restored, process took " + sw.Elapsed.TotalSeconds + " seconds.");
                sw.Stop();
                return failedTypes.Count == 0;
            }
            catch (Exception exc)
            {
                failedException = exc;
                logger.LogError($"Data migration failed!! " + exc);
                return false;
            }
        }

        private bool TryTruncateTable(SqlConnection con, string table)
        {
            try
            {
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = $"truncate table {table};";
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch (Exception exc)
            {
                logger.LogError($"Failed to truncate table {table}! Table wont be migrated!! Exc: " + exc);
                return false;
            }
        }

        public void MigrateTest(IEntityRestorePoint restorePoint)
        {
            try
            {
                var sw = new Stopwatch();
                sw.Start();
                var queryBuilder = new QueryBuilder();
                var users = restorePoint.Get<User>();
                var characters = restorePoint.Get<Character>();
                var skills = restorePoint.Get<Skills>();
                var marketplaceItems = restorePoint.Get<MarketItem>();
                var syntyAppearance = restorePoint.Get<SyntyAppearance>();
                var resources = restorePoint.Get<Resources>();
                var charStates = restorePoint.Get<CharacterState>();
                var charStats = restorePoint.Get<Statistics>();
                var iitems = restorePoint.Get<InventoryItem>();
                IReadOnlyList<InventoryItem> invItems = Merge(iitems);
                var villages = restorePoint.Get<Village>();
                var villageHouses = restorePoint.Get<VillageHouse>();

                var failedCount = 0;
                logger?.LogInformation("Migrating user data...");
                var index = 0;
                foreach (var character in characters)
                {
                    if (!string.IsNullOrEmpty(character.Identifier)
                        && int.TryParse(character.Name, out var twitchId))
                    {
                        var user = users.FirstOrDefault(x => x.UserId == character.Name);
                        if (user != null)
                        {
                            var otherChars = characters.Where(x => x.UserId == user.Id).ToList();
                            if (otherChars.Any(x => x.Id != character.Id))
                                continue;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    try
                    {
                        var cmdQuery = BuildInsertQuery(
                            queryBuilder,
                            character,
                            users,
                            marketplaceItems,
                            skills,
                            syntyAppearance,
                            resources,
                            charStates,
                            charStats,
                            invItems,
                            villages,
                            villageHouses);

                        //if (results < 7)
                        //{
                        //    failedCount++;
                        //    logger.LogError($"{character.Name} failed to migrate!!");
                        //}
                    }
                    catch (Exception exc)
                    {
                        logger?.LogError($"Failed to migrate player!!: " + exc);
                    }
                    ++index;
                }


                sw.Stop();
                logger?.LogInformation($"Data migration inserted {characters.Count} characters, {failedCount} failed inserts and took a total of: " + sw.Elapsed);
            }
            catch (Exception exc)
            {
                logger?.LogError($"Data migration failed!! " + exc);
            }
        }

        private IReadOnlyList<InventoryItem> Merge(IReadOnlyList<InventoryItem> readOnlyLists)
        {
            var before = readOnlyLists.Count;
            List<InventoryItem> items = new List<InventoryItem>();
            if (readOnlyLists.Count > 0)
            {
                foreach (var playerItems in readOnlyLists.GroupBy(x => x.CharacterId))
                {
                    var dupeFix = new List<InventoryItem>();
                    foreach (var inv in playerItems.GroupBy(x => x.Id))
                    {
                        dupeFix.Add(inv.OrderByDescending(x => x.Amount).FirstOrDefault());
                    }

                    foreach (var itemStacks in dupeFix.GroupBy(x => x.ItemId))
                    {
                        var amount = itemStacks.FirstOrDefault().Amount;
                        var itemId = itemStacks.Key;
                        var characterId = itemStacks.FirstOrDefault().CharacterId;

                        var equipped = itemStacks.FirstOrDefault(x => x.Equipped);
                        if (equipped != null)
                        {
                            equipped.Amount = 1;
                            --amount;
                            items.Add(equipped);
                        }

                        var stack = itemStacks.FirstOrDefault(x => !x.Equipped);
                        if (stack != null)
                        {
                            stack.Amount += amount;
                            if (stack.Amount > 10000)
                                stack.Amount = 10;

                            items.Add(stack);
                        }
                    }
                }
            }
            return items;
        }

        private string[] BuildInsertQuery(
            QueryBuilder qb, IReadOnlyList<IEntity> entities, int batchSize = 1000)
        {
            var queries = new List<string>();
            var i = 0;
            var size = batchSize;
            while (true)
            {
                var batch = entities.Skip(i * size).Take(size).ToList();
                if (batch.Count == 0) break;
                queries.Add(qb.InsertMany(batch));
                ++i;
            }
            return queries.ToArray();
        }

        private string[] BuildInsertQuery(
            QueryBuilder qb,
            Character character,
            IReadOnlyList<User> users,
            IReadOnlyList<MarketItem> marketplaceItems,
            IReadOnlyList<Skills> skills,
            IReadOnlyList<SyntyAppearance> syntyAppearances,
            IReadOnlyList<Resources> resources,
            IReadOnlyList<CharacterState> charStates,
            IReadOnlyList<Statistics> charStats,
            IReadOnlyList<InventoryItem> invItems,
            IReadOnlyList<Village> villages,
            IReadOnlyList<VillageHouse> villageHouses)
        {
            var queries = new List<string>();
            var query = new StringBuilder();

            User user = users.FirstOrDefault(x => x.Id == character.UserId);
            if (user == null) return Array.Empty<string>();

            if (importedUsers.Add(user.Id))
            {
                query.AppendLine(qb.Insert(user));
            }


            Skills skill = skills.FirstOrDefault(x => x.Id == character.SkillsId);
            if (skill != null)
                query.AppendLine(qb.Insert(skill));

            SyntyAppearance appearance = syntyAppearances.FirstOrDefault(x => x.Id == character.SyntyAppearanceId);
            if (appearance != null)
                query.AppendLine(qb.Insert(appearance));

            CharacterState state = charStates.FirstOrDefault(x => x.Id == character.StateId);
            if (state != null)
                query.AppendLine(qb.Insert(state));

            Statistics statistics = charStats.FirstOrDefault(x => x.Id == character.StatisticsId); ;
            if (statistics != null)
                query.AppendLine(qb.Insert(statistics));

            query.AppendLine(qb.Insert(character));

            queries.Add(query.ToString());
            query.Clear();


            InventoryItem[] inventoryItems = invItems.Where(x => x.CharacterId == character.Id).ToArray();
            if (inventoryItems.Length > 0)
            {
                if (inventoryItems.Length > 100)
                {
                    for (var i = 0; i < inventoryItems.Length;)
                    {
                        var take = inventoryItems.Skip(i * 100).Take(100).ToArray();

                        queries.Add(qb.InsertMany(take));

                        i += take.Length;
                    }
                }
                else
                {
                    queries.Add(qb.InsertMany(inventoryItems));
                }
            }

            MarketItem[] marketItems = marketplaceItems.Where(x => x.SellerCharacterId == character.Id).ToArray();
            if (marketItems.Length > 0)
            {
                foreach (var ma in marketItems)
                {
                    if (ma.Amount > 10_000_0000)
                        ma.Amount = 10_000_000;
                    if (ma.PricePerItem > 10_000_0000)
                        ma.PricePerItem = 10_000_000;
                }

                if (marketItems.Length > 100)
                {
                    for (var i = 0; i < marketItems.Length;)
                    {
                        var take = marketItems.Skip(i * 100).Take(100).ToArray();

                        queries.Add(qb.InsertMany(take));

                        i += take.Length;
                    }
                }
                else
                {
                    queries.Add(qb.InsertMany(marketItems));
                }
            }

            Village village = villages.FirstOrDefault(x => x.UserId == user.Id);
            if (village != null)
                queries.Add(qb.Insert(village));

            if (village != null)
            {
                VillageHouse[] villageHouse = villageHouses.Where(x => x.VillageId == village.Id).ToArray();
                if (villageHouse.Length > 0)
                {
                    if (villageHouse.Length > 100)
                    {
                        for (var i = 0; i < villageHouse.Length;)
                        {
                            var take = villageHouse.Skip(i * 100).Take(100).ToArray();

                            queries.Add(qb.InsertMany(take));

                            i += take.Length;
                        }
                    }
                    else
                    {
                        queries.Add(qb.InsertMany(villageHouse));
                    }
                }
            }

            return queries.ToArray();
        }

        private class QueryBuilder
        {
            private readonly Type[] numericTypes = new Type[] {
            typeof(byte), typeof(sbyte), typeof(ushort), typeof(short), typeof(uint), typeof(int), typeof(ulong), typeof(long), typeof(decimal), typeof(float), typeof(double),
            typeof(byte?), typeof(sbyte?), typeof(ushort?), typeof(short?), typeof(uint?), typeof(int?), typeof(ulong?), typeof(long?), typeof(decimal?), typeof(float?), typeof(double?)
        };
            private readonly ConcurrentDictionary<Type, PropertyInfo[]> propertyCache = new ConcurrentDictionary<Type, PropertyInfo[]>();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private IEnumerable<string> GetSqlReadyPropertyValues(IEntity entity, PropertyInfo[] properties)
            {
                return properties.Select(x => GetSqlReadyPropertyValue(x.PropertyType, x.GetValue(entity)));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private IEnumerable<string> GetSqlReadyPropertySet(IEntity entity, PropertyInfo[] properties)
            {
                return properties.Select(x => x.Name + "=" + GetSqlReadyPropertyValue(x.PropertyType, x.GetValue(entity)));
            }

            private string GetSqlReadyPropertyValue(Type type, object value)
            {
                if (value == null) return "NULL";
                if (type == typeof(string) || type == typeof(char)
                    || type == typeof(DateTime) || type == typeof(TimeSpan)
                    || type == typeof(DateTime?) || type == typeof(TimeSpan?)
                    || type == typeof(Guid?) || type == typeof(Guid))
                {

                    return $"'{Sanitize(value?.ToString())}'";
                }

                if (type.IsEnum)
                {
                    return ((int)value).ToString();
                }

                if (numericTypes.Any(x => x == type))
                {
                    return value.ToString().Replace(',', '.').Replace('−', '-');
                }

                if (typeof(bool) == type)
                {
                    return (bool)value == true ? "1" : "0";
                }

                if (typeof(bool?) == type)
                {
                    var b = (value as bool?);
                    return b.GetValueOrDefault() ? "1" : "0";
                }

                return "NULL";
            }

            private string Sanitize(string value)
            {
                // TODO: Implement
                //       We should be using sqlparameters but how do we bulk that properly?
                return value?.Replace("'", "''");
            }
            private PropertyInfo[] GetProperties(Type type)
            {
                if (propertyCache.TryGetValue(type, out var properties))
                {
                    return properties;
                }

                return propertyCache[type] = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            }

            private PropertyInfo GetProperty(Type type, string propertyName)
            {
                return GetProperties(type).FirstOrDefault(x => x.Name == propertyName);
            }
            internal string Insert<T>(T entity) where T : IEntity
            {
                var type = entity.GetType();
                var props = GetProperties(type);
                var propertyNames = string.Join(", ", props.Select(x => "[" + x.Name + "]"));
                var values = string.Join(",", GetSqlReadyPropertyValues(entity, props));

                var sb = new StringBuilder();
                sb.AppendLine("INSERT INTO [" + type.Name + "]");
                sb.AppendLine("(" + propertyNames + ")");
                sb.AppendLine("VALUES (" + values + ");");
                return sb.ToString();
            }

            internal string InsertMany(IEnumerable<IEntity> entity)
            {
                var e = entity.FirstOrDefault();
                if (e == null)
                {
                    return null;
                }

                var type = e.GetType();

                var props = GetProperties(type);
                var propertyNames = string.Join(", ", props.Select(x => "[" + x.Name + "]"));
                var values = string.Join(",\r\n", entity.Select(x => "(" + string.Join(",", GetSqlReadyPropertyValues(x, props)) + ")"));

                var sb = new StringBuilder();
                sb.Append("INSERT INTO [" + type.Name + "] ");
                sb.AppendLine("(" + propertyNames + ") VALUES");
                sb.AppendLine(values);
                return sb.ToString();
            }
            internal string InsertMany<T>(IEnumerable<T> entity) where T : IEntity
            {
                var type = typeof(T);
                var props = GetProperties(type);
                var propertyNames = string.Join(", ", props.Select(x => "[" + x.Name + "]"));
                var values = string.Join(",\r\n", entity.Select(x => "(" + string.Join(",", GetSqlReadyPropertyValues(x, props)) + ")"));

                var sb = new StringBuilder();
                sb.Append("INSERT INTO [" + type.Name + "] ");
                sb.AppendLine("(" + propertyNames + ") VALUES");
                sb.AppendLine(values);
                return sb.ToString();
            }

            internal string InsertIfNotExists<T>(T entity, string keyName = "Id") where T : IEntity
            {
                var type = entity.GetType();
                var props = GetProperties(type);
                var propertyNames = string.Join(", ", props.Select(x => "[" + x.Name + "]"));
                var values = string.Join(",", GetSqlReadyPropertyValues(entity, props));
                var propertySets = string.Join(",", GetSqlReadyPropertySet(entity, props.Where(x => x.Name != "Id").ToArray()));

                var sb = new StringBuilder();
                sb.AppendLine("MERGE " + type.Name + " x");
                sb.AppendLine("USING (" + propertySets + ") temp");
                sb.AppendLine("ON temp." + keyName + " = x." + keyName);
                sb.AppendLine("WHEN NOT MATCHED THEN");
                sb.AppendLine("INSERT (" + propertyNames + ")");
                sb.AppendLine("VALUES (" + values + ");");
                return sb.ToString();

                /*         
                    MERGE User u
                    USING (Id = user.Id, UserId = user.UserId, DisplayName = user.DisplayName, IsAdmin = user.IsAdmin, IsModerator = user.IsModerator, Created = user.Created) temp
                    ON temp.UserId = u.UserId
                    WHEN NOT MATCHED THEN
                    INSERT (Id, UserId, DisplayName, IsAdmin, IsModerator, Created) VALUES(temp.Id, temp.UserId, temp.DisplayName, temp.IsAdmin, temp.IsModerator, temp.Created);
                 */
            }
        }
    }
}
