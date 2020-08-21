namespace RavenNest.BusinessLogic.Data
{
    public interface IGameDataMigration
    {
        void Migrate(IRavenfallDbContextProvider db, IEntityRestorePoint restorePoint);
    }
}