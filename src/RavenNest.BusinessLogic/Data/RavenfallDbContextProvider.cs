using Microsoft.Extensions.Options;

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
            return new RavenfallDbContext(settings.DbConnectionString);
        }
    }
}