namespace RavenNest.BusinessLogic.Providers
{
    public interface IMemoryFileCache
    {
        void Remove(string key);
        bool TryGetValue(string key, out byte[] fileContent);
        byte[] Set(string key, byte[] fileContent);
    }
}
