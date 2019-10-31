using System.Data.SqlClient;

namespace RavenNest.BusinessLogic.Data
{
    public class SqlSaveQuery
    {
        public SqlSaveQuery(string command)
        {
            Command = command;
        }

        public string Command { get; }
    }
}