using System.Data.SqlClient;

namespace RavenNest.BusinessLogic.Data
{
    public interface IRavenfallDbContextProvider
    {
        SqlConnection GetConnection();
        RavenfallDbContext Get();
    }
}