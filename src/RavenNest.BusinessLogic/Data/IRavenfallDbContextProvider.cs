namespace RavenNest.BusinessLogic.Data
{
    public interface IRavenfallDbContextProvider
    {
        RavenfallDbContext Get();
    }
}