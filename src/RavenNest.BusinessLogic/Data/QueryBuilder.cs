using Microsoft.EntityFrameworkCore;

namespace RavenNest.BusinessLogic.Data
{
    public class QueryBuilder : IQueryBuilder
    {
        public SqlSaveQuery Build(EntityStoreItems saveData)
        {
            switch (saveData.State)
            {
                case EntityState.Added: return BuildInsertQuery(saveData);
                case EntityState.Deleted: return BuildDeleteQuery(saveData);
                case EntityState.Modified: return BuildUpdateQuery(saveData);
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