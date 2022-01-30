namespace GameDataSimulation
{
    public class MiningDropSimulationSettings : SimulationSettings
    {
        public int MiningLevel { get => Get<int>(nameof(MiningLevel)); init => Set(nameof(MiningLevel), value); }
        public int SimulateGamePlayHours { get => Get<int>(nameof(SimulateGamePlayHours)); init => Set(nameof(SimulateGamePlayHours), value); }
        public int TimeScaleFactor { get => Get<int>(nameof(TimeScaleFactor)); init => Set(nameof(TimeScaleFactor), value); }
    }
}
