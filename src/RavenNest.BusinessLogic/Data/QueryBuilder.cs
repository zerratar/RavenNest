using Microsoft.EntityFrameworkCore;

namespace RavenNest.BusinessLogic.Data
{
    public class QueryBuilder : IQueryBuilder
    {
        public SqlSaveQuery Build(EntityStoreItems saveData)
        {
            switch (saveData.State)
            {
                case RavenNest.DataModels.EntityState.Added: return BuildInsertQuery(saveData);
                case RavenNest.DataModels.EntityState.Deleted: return BuildDeleteQuery(saveData);
                case RavenNest.DataModels.EntityState.Modified: return BuildUpdateQuery(saveData);
                default: return null;
            }
        }

        private SqlSaveQuery BuildUpdateQuery(EntityStoreItems saveData)
        {
            return null;
        }

        private SqlSaveQuery BuildDeleteQuery(EntityStoreItems saveData)
        {
            return null;
        }

        private SqlSaveQuery BuildInsertQuery(EntityStoreItems saveData)
        {
            return null;
        }
    }
}