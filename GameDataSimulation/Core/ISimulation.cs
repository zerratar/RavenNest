namespace GameDataSimulation
{
    public interface ISimulation
    {
        SimulationResult Run(ISimulationSettings settings);
    }
}
