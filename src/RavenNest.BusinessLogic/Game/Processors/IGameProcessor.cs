using System.Threading;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Game.Processors
{
    public interface IGameProcessor
    {
        void Process(CancellationTokenSource cts);
    }
}
