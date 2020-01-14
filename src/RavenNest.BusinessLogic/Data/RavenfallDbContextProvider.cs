using Microsoft.Extensions.Options;
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
    }
}