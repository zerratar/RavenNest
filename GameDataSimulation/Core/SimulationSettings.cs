namespace GameDataSimulation
{
    public class SimulationSettings : ISimulationSettings
    {
        private readonly Dictionary<string, ISimulationSettingsValue> values
            = new Dictionary<string, ISimulationSettingsValue>();
        public ISimulationSettingsValue this[string key]
        {
            get
            {
                if (values.TryGetValue(key, out var res))
                {
                    return res;
                }

                return (values[key] = new SimulationSettingsValue());
            }
            set => values[key] = value ?? new SimulationSettingsValue();
        }

        public T? Get<T>(string key)
        {
            return this[key].Get<T>();
        }

        public void Set<T>(string key, T value)
        {
            this[key].Set(value);
        }
    }
}
