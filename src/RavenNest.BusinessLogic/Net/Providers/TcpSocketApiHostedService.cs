using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace RavenNest.BusinessLogic.Net
{
    public class TcpSocketApiHostedService : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

        }
    }
}
