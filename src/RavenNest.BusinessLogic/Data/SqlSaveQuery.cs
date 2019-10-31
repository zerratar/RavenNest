using System.Data.SqlClient;

namespace RavenNest.BusinessLogic.Data
{
    public class SqlSaveQuery
    {
        public SqlSaveQuery(string command, SqlParameter[] parameters)
        {
            Command = command;
            Parameters = parameters;
        }

        public string Command { get; }
        public SqlParameter[] Parameters { get; }
    }
}