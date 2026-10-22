using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Data
{
    public class QueryBuilder : IQueryBuilder
    {
        private readonly ConcurrentDictionary<Type, PropertyInfo[]> propertyCache
            = new ConcurrentDictionary<Type, PropertyInfo[]>();

        private readonly Type[] numericTypes = new Type[] {
            typeof(byte), typeof(sbyte), typeof(ushort), typeof(short), typeof(uint), typeof(int), typeof(ulong), typeof(long), typeof(decimal), typeof(float), typeof(double),
            typeof(byte?), typeof(sbyte?), typeof(ushort?), typeof(short?), typeof(uint?), typeof(int?), typeof(ulong?), typeof(long?), typeof(decimal?), typeof(float?), typeof(double?)
        };

        /// <summary>SQL Server takes at most 2100 parameters in one request.</summary>
        private const int MaxParameters = 2000;

        /// <summary>
        /// The save as parameterized commands. Each command's text depends only on the entity type and how many rows
        /// it carries, so SQL Server reuses its plans. The values used to be written into the SQL, which made every
        /// save a new statement with a new cached plan: in a two hour soak the plan cache grew to 5.6 GB, pushed the
        /// data out of SQL Server's memory, and a database save went from about 1 s to 5-60 s after an hour.
        /// </summary>
        public SqlSaveQuery Build(EntityStoreItems saveData)
        {
            if (saveData?.Entities == null || saveData.Entities.Count == 0)
            {
                return null;
            }

            switch (saveData.State)
            {
                case EntityState.Added: return new SqlSaveQuery(BuildInsertCommands(saveData), () => BuildInsertQuery(saveData).Command);
                case EntityState.Deleted: return new SqlSaveQuery(BuildDeleteCommands(saveData), () => BuildDeleteQuery(saveData).Command);
                case EntityState.Modified: return new SqlSaveQuery(BuildUpdateCommands(saveData), () => BuildUpdateQuery(saveData).Command);
                default: return null;
            }
        }

        private static Type EntityType(IEntity entity)
        {
            var type = entity.GetType();
            return type.FullName.Contains("Extended") ? type.BaseType : type;
        }

        private List<SqlSaveCommand> BuildUpdateCommands(EntityStoreItems saveData)
        {
            var commands = new List<SqlSaveCommand>();
            foreach (var group in saveData.Entities.Where(x => x != null).GroupBy(EntityType))
            {
                var type = group.Key;
                var idProperty = GetProperty(type, "Id");
                var sets = GetProperties(type).Where(x => x.Name != "Id").ToArray();
                var rowsPerCommand = Math.Max(1, MaxParameters / (sets.Length + 1));
                foreach (var chunk in group.Chunk(rowsPerCommand))
                {
                    var sb = new StringBuilder();
                    var parameters = new List<SqlParameter>(chunk.Length * (sets.Length + 1));
                    foreach (var entity in chunk)
                    {
                        sb.Append("UPDATE [").Append(type.Name).Append("] SET ");
                        for (var i = 0; i < sets.Length; ++i)
                        {
                            if (i > 0) sb.Append(',');
                            sb.Append(sets[i].Name).Append(" = ").Append(Add(parameters, sets[i].PropertyType, sets[i].GetValue(entity)));
                        }
                        sb.Append(" WHERE Id = ").Append(Add(parameters, idProperty.PropertyType, idProperty.GetValue(entity))).Append(";\n");
                    }
                    commands.Add(new SqlSaveCommand(sb.ToString(), parameters));
                }
            }
            return commands;
        }

        private List<SqlSaveCommand> BuildDeleteCommands(EntityStoreItems saveData)
        {
            var commands = new List<SqlSaveCommand>();
            foreach (var group in saveData.Entities.Where(x => x != null).GroupBy(EntityType))
            {
                var type = group.Key;
                var idProperty = GetProperty(type, "Id");
                foreach (var chunk in group.Chunk(MaxParameters))
                {
                    var parameters = new List<SqlParameter>(chunk.Length);
                    var ids = chunk.Select(entity => Add(parameters, idProperty.PropertyType, idProperty.GetValue(entity))).ToList();
                    commands.Add(new SqlSaveCommand("DELETE FROM [" + type.Name + "] WHERE Id IN (" + string.Join(",", ids) + ");", parameters));
                }
            }
            return commands;
        }

        private List<SqlSaveCommand> BuildInsertCommands(EntityStoreItems saveData)
        {
            var commands = new List<SqlSaveCommand>();
            foreach (var group in saveData.Entities.Where(x => x != null).GroupBy(EntityType))
            {
                var type = group.Key;
                var properties = GetProperties(type);
                var propertyNames = string.Join(", ", properties.Select(x => x.Name));
                // a VALUES list takes at most 1000 rows
                var rowsPerCommand = Math.Clamp(MaxParameters / Math.Max(1, properties.Length), 1, 1000);
                foreach (var chunk in group.Chunk(rowsPerCommand))
                {
                    var sb = new StringBuilder();
                    var parameters = new List<SqlParameter>(chunk.Length * properties.Length);
                    sb.Append("INSERT INTO [").Append(type.Name).Append("] (").Append(propertyNames).Append(") VALUES ");
                    for (var r = 0; r < chunk.Length; ++r)
                    {
                        if (r > 0) sb.Append(',');
                        sb.Append('(');
                        for (var i = 0; i < properties.Length; ++i)
                        {
                            if (i > 0) sb.Append(',');
                            sb.Append(Add(parameters, properties[i].PropertyType, properties[i].GetValue(chunk[r])));
                        }
                        sb.Append(')');
                    }
                    sb.Append(';');
                    commands.Add(new SqlSaveCommand(sb.ToString(), parameters));
                }
            }
            return commands;
        }

        /// <summary>
        /// Adds a parameter for a value and returns its name. The conversions match the SQL the values used to be
        /// written as (NULL, NaN as 0, enums as numbers, text as text), and every parameter has a fixed type and
        /// size, because a parameter declared differently is a different plan.
        /// </summary>
        private static string Add(List<SqlParameter> parameters, Type type, object value)
        {
            var name = "@p" + parameters.Count;
            parameters.Add(CreateParameter(name, type, value));
            return name;
        }

        internal static SqlParameter CreateParameter(string name, Type declared, object value)
        {
            var type = Nullable.GetUnderlyingType(declared) ?? declared;
            if (value == null)
            {
                return new SqlParameter(name, SqlDbType.NVarChar, -1) { Value = DBNull.Value };
            }

            if (type == typeof(string) || type == typeof(char) || type == typeof(TimeSpan))
            {
                return new SqlParameter(name, SqlDbType.NVarChar, -1) { Value = value.ToString() };
            }
            if (type == typeof(DateTime))
            {
                return new SqlParameter(name, SqlDbType.DateTime2) { Value = value };
            }
            if (type == typeof(Guid))
            {
                return new SqlParameter(name, SqlDbType.UniqueIdentifier) { Value = value };
            }
            if (type.IsEnum)
            {
                return new SqlParameter(name, SqlDbType.Int) { Value = Convert.ToInt32(value) };
            }
            if (type == typeof(bool))
            {
                return new SqlParameter(name, SqlDbType.Bit) { Value = (bool)value };
            }
            if (type == typeof(double))
            {
                var d = (double)value;
                return new SqlParameter(name, SqlDbType.Float) { Value = double.IsNaN(d) || double.IsInfinity(d) ? 0d : d };
            }
            if (type == typeof(float))
            {
                var f = (float)value;
                return new SqlParameter(name, SqlDbType.Real) { Value = float.IsNaN(f) || float.IsInfinity(f) ? 0f : f };
            }
            if (type == typeof(int) || type == typeof(short) || type == typeof(byte) || type == typeof(sbyte) || type == typeof(ushort))
            {
                return new SqlParameter(name, SqlDbType.Int) { Value = Convert.ToInt32(value) };
            }
            if (type == typeof(long) || type == typeof(uint))
            {
                return new SqlParameter(name, SqlDbType.BigInt) { Value = Convert.ToInt64(value) };
            }
            if (type == typeof(ulong) || type == typeof(decimal))
            {
                return new SqlParameter(name, SqlDbType.Decimal) { Precision = 38, Scale = 10, Value = Convert.ToDecimal(value) };
            }

            // anything else was written as NULL
            return new SqlParameter(name, SqlDbType.NVarChar, -1) { Value = DBNull.Value };
        }

        // The same saves as plain SQL with the values written out: only for the logs and the error query file now.

        private SqlSaveQuery BuildUpdateQuery(EntityStoreItems saveData)
        {
            StringBuilder sb = new StringBuilder();
            //sb.AppendLine("BEGIN TRANSACTION;");
            foreach (var entity in saveData.Entities)
            {
                var type = entity.GetType();
                if (type.FullName.Contains("Extended"))
                    type = type.BaseType;

                var properties = GetProperties(type);
                var idProperty = GetProperty(type, "Id");
                var propertySets = GetSqlReadyPropertySet(entity, properties.Where(x => x.Name != "Id").ToArray());
                sb.Append($"UPDATE [{type.Name}] ");
                sb.Append($"SET {string.Join(",", propertySets)} ");
                sb.AppendLine($"WHERE Id = {GetSqlReadyPropertyValue(idProperty.PropertyType, idProperty.GetValue(entity))};");
            }

            //sb.AppendLine("COMMIT;");
            return new SqlSaveQuery(sb.ToString());
        }

        private SqlSaveQuery BuildDeleteQuery(EntityStoreItems saveData)
        {
            StringBuilder sb = new StringBuilder();
            //sb.AppendLine("BEGIN TRANSACTION;");
            foreach (var group in saveData.Entities.GroupBy(x => x.GetType()))
            {
                var type = group.Key;
                if (type.FullName.Contains("Extended"))
                    type = type.BaseType;

                var idProperty = GetProperty(type, "Id");
                //var properties = GetProperties(type);            
                sb.AppendLine($"DELETE FROM [{type.Name}] WHERE ");

                var wheres = group.Select(entity => "(Id = " + GetSqlReadyPropertyValue(idProperty.PropertyType, idProperty.GetValue(entity)) + ")").ToList();
                sb.Append(string.Join(" OR \r\n", wheres));

                sb.AppendLine(";");
            }
            //sb.AppendLine("COMMIT;");
            return new SqlSaveQuery(sb.ToString());
        }

        private SqlSaveQuery BuildInsertQuery(EntityStoreItems saveData)
        {
            StringBuilder sb = new StringBuilder();
            //sb.AppendLine("BEGIN TRANSACTION;");
            foreach (var group in saveData.Entities.GroupBy(x => x.GetType()))
            {
                var type = group.Key;
                if (type.FullName.Contains("Extended"))
                    type = type.BaseType;

                var properties = GetProperties(type);
                var propertyNames = string.Join(", ", properties.Select(x => x.Name));
                sb.AppendLine($"INSERT INTO [{type.Name}] ({propertyNames}) VALUES ");

                var rows = group.Select(x => "(" + string.Join(",", GetSqlReadyPropertyValues(x, properties)) + ")");
                var line = string.Join(",\r\n", rows);
                sb.Append(line);
                sb.AppendLine(";");
            }
            //sb.AppendLine("COMMIT;");
            return new SqlSaveQuery(sb.ToString());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IEnumerable<string> GetSqlReadyPropertyValues(IEntity entity, PropertyInfo[] properties)
        {
            return properties.Select(x => GetSqlReadyPropertyValue(x.PropertyType, x.GetValue(entity)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IEnumerable<string> GetSqlReadyPropertySet(IEntity entity, PropertyInfo[] properties)
        {
            return properties.Select(x => x.Name + " = " + GetSqlReadyPropertyValue(x.PropertyType, x.GetValue(entity)));
        }

        private string GetSqlReadyPropertyValue(Type type, object value)
        {
            var val = GetSqlReadyPropertyValueRaw(type, value);
            if (val == "NaN")
            {
                return "0";
            }
            return val;
        }

        private string GetSqlReadyPropertyValueRaw(Type type, object value)
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
                return value.ToString().Replace(',', '.');
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

            if (typeof(int) == type ||
                typeof(long) == type ||
                typeof(short) == type ||
                typeof(byte) == type ||
                typeof(uint) == type ||
                typeof(ulong) == type ||
                typeof(ushort) == type ||
                typeof(sbyte) == type)
            {
                var val = value.ToString();
                if (val == "NaN") val = "0";
                return val;
            }

            return "NULL";
        }

        private string Sanitize(string value)
        {
            // TODO: Implement
            //       We should be using sqlparameters but how do we bulk that properly?
            return value?.Replace("'", "''");
        }

        private Dictionary<string, object> GetKeyValueMap(IEntity entity, PropertyInfo[] properties)
        {
            var dict = new Dictionary<string, object>();
            foreach (var property in properties)
            {
                dict[property.Name] = property.GetValue(entity);
            }
            return dict;
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
    }
}
