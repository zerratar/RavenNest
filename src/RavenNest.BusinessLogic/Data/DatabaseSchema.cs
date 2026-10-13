using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.BusinessLogic.Data
{
    /// <summary>
    /// Brings the database up to the entity model at startup: creates missing tables and adds missing
    /// columns, so a new [PersistentData] field or entity no longer needs a hand-written SQL file run
    /// before deploying.
    /// </summary>
    /// <remarks>
    /// It only ever adds. It never drops, renames or changes a column, and never touches data. The model
    /// comes from EF (RavenfallDbContext, including its OnModelCreating overrides), so the column types are
    /// the ones EF expects when it loads. A new nullable column is added as NULL. A new column that cannot
    /// be null gets a default (0, an empty string, an empty id or a fixed date), so existing rows stay
    /// valid. Fill new data from code after loading, the way the Ensure* methods in GameData do.
    /// </remarks>
    public static class DatabaseSchema
    {
        public static void Ensure(IRavenfallDbContextProvider db, ILogger logger)
        {
            try
            {
                using var ctx = db.Get();
                var model = ctx.GetService<IDesignTimeModel>().Model.GetRelationalModel();

                using var connection = db.GetConnection();
                connection.Open();

                var existing = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                using (var query = connection.CreateCommand())
                {
                    query.CommandText = "SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS";
                    using var reader = query.ExecuteReader();
                    while (reader.Read())
                    {
                        var key = reader.GetString(0) + "." + reader.GetString(1);
                        if (!existing.TryGetValue(key, out var columns))
                        {
                            existing[key] = columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        }

                        columns.Add(reader.GetString(2));
                    }
                }

                var changes = 0;
                foreach (var table in model.Tables)
                {
                    var schema = string.IsNullOrEmpty(table.Schema) ? "dbo" : table.Schema;
                    if (!existing.TryGetValue(schema + "." + table.Name, out var columns))
                    {
                        Execute(connection, CreateTable(schema, table));
                        logger.LogWarning("Database schema: created table {Schema}.{Table}", schema, table.Name);
                        changes++;
                        continue;
                    }

                    foreach (var column in table.Columns)
                    {
                        if (columns.Contains(column.Name)) continue;
                        var definition = ColumnDefinition(column, forNewColumn: true);
                        if (definition == null)
                        {
                            logger.LogError("Database schema: {Schema}.{Table}.{Column} ({Type}) is missing and has no safe default; add it by hand.",
                                schema, table.Name, column.Name, column.StoreType);
                            continue;
                        }

                        Execute(connection, $"ALTER TABLE [{schema}].[{table.Name}] ADD {definition}");
                        logger.LogWarning("Database schema: added column {Schema}.{Table}.{Column} ({Type})", schema, table.Name, column.Name, column.StoreType);
                        changes++;
                    }
                }

                logger.LogInformation("Database schema checked, {Changes} change(s).", changes);
            }
            catch (Exception exc)
            {
                // Loading will fail with its own error if something is still missing; this keeps the reason visible.
                logger.LogError("Database schema check failed: " + exc);
            }
        }

        private static string CreateTable(string schema, ITable table)
        {
            var parts = table.Columns.Select(c => ColumnDefinition(c, forNewColumn: false)).ToList();
            var key = table.PrimaryKey;
            if (key != null && key.Columns.Count > 0)
            {
                parts.Add($"CONSTRAINT [PK_{table.Name}] PRIMARY KEY CLUSTERED ({string.Join(", ", key.Columns.Select(c => "[" + c.Name + "]"))})");
            }

            return $"CREATE TABLE [{schema}].[{table.Name}] ({string.Join(", ", parts)})";
        }

        /// <summary>
        /// "[Name] type NULL" or "[Name] type NOT NULL". A NOT NULL column added to an existing table gets a
        /// default so the rows already there are valid; null when no safe default is known for its type.
        /// </summary>
        private static string ColumnDefinition(IColumn column, bool forNewColumn)
        {
            var name = "[" + column.Name + "] " + column.StoreType;
            if (column.IsNullable) return name + " NULL";
            if (!forNewColumn) return name + " NOT NULL";
            var value = DefaultFor(column.StoreType);
            return value == null ? null : $"{name} NOT NULL CONSTRAINT [DF_{column.Table.Name}_{column.Name}] DEFAULT ({value})";
        }

        private static string DefaultFor(string storeType)
        {
            var type = (storeType ?? string.Empty).ToLowerInvariant();
            if (type.StartsWith("nvarchar") || type.StartsWith("varchar") || type.StartsWith("nchar") || type.StartsWith("char")) return "N''";
            if (type == "uniqueidentifier") return "'00000000-0000-0000-0000-000000000000'";
            if (type.StartsWith("datetime") || type == "date") return "'2000-01-01'";
            if (type == "bit" || type == "int" || type == "bigint" || type == "smallint" || type == "tinyint" ||
                type == "float" || type == "real" || type.StartsWith("decimal") || type.StartsWith("numeric")) return "0";
            return null;
        }

        private static void Execute(System.Data.SqlClient.SqlConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }
}
