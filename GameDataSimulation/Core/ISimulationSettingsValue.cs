namespace GameDataSimulation
{
    public interface ISimulationSettingsValue
    {
        T Get<T>();
        void Set<T>(T value);
    }
}
