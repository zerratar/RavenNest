using Microsoft.Extensions.Options;
using System;
using System.Data.SqlClient;

namespace RavenNest.BusinessLogic.Data
{
    public class RavenfallDbContextProvider : IRavenfallDbContextProvider
    {
        private readonly AppSettings settings;

        public RavenfallDbContextProvider(IOptions<AppSettings> settings)
        {
            this.settings = settings.Value;
        }

        public RavenfallDbContext Get()
        {
            var ctx = new RavenfallDbContext(settings.DbConnectionString);
            ctx.ChangeTracker.AutoDetectChangesEnabled = false;
            return ctx;
        }

        public SqlConnection GetConnection()
        {
            return new SqlConnection(settings.DbConnectionString);
        }

        public string GetDbName()
        {
            return settings.DbConnectionString.Split("Catalog=", System.StringSplitOptions.RemoveEmptyEntries)[1].Split(';')[0];
        }
    }
}
