﻿namespace RavenNest.BusinessLogic.Data
{
    public interface IQueryBuilder
    {
        SqlSaveQuery Build(EntityStoreItems saveData, bool DiffDateTimeFormat = false);
    }
}
