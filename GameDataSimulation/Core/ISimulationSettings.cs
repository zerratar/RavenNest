namespace GameDataSimulation
{
    public interface ISimulationSettings
    {
        T? Get<T>(string key);
        void Set<T>(string key, T value);

        ISimulationSettingsValue this[string key] { get; set; }
    }
}
