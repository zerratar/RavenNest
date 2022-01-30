namespace GameDataSimulation
{
    public class SimulationSettingsValue : ISimulationSettingsValue
    {
        private object _value;
        public T Get<T>()
        {
            if (_value is T val)
                return val;

            return default;
        }

        public void Set<T>(T value)
        {
            _value = value;
        }
    }
}
