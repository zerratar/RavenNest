using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using System;

namespace RavenNest.Blazor
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                    .UseStartup<Startup>()
                    .UseKestrel(options =>
                    {
                        options.Limits.MaxConcurrentConnections = long.MaxValue;
                        options.Limits.MaxConcurrentUpgradedConnections = long.MaxValue;
                        options.Limits.MinRequestBodyDataRate = new MinDataRate(bytesPerSecond: 10, gracePeriod: TimeSpan.FromSeconds(10));
                        options.Limits.MinResponseDataRate = new MinDataRate(bytesPerSecond: 10, gracePeriod: TimeSpan.FromSeconds(10));
                    });
                });
    }
}
