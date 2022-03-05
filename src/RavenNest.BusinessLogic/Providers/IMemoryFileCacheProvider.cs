namespace RavenNest.BusinessLogic.Providers
{
    public interface IMemoryFileCacheProvider
    {
        IMemoryFileCache Get(string key, string extension = ".bin");
    }
}
