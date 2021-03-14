using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
                        options.Limits.MinRequestBodyDataRate = new MinDataRate(bytesPerSecond: 50, gracePeriod: TimeSpan.FromSeconds(10));
                        options.Limits.MinResponseDataRate = new MinDataRate(bytesPerSecond: 50, gracePeriod: TimeSpan.FromSeconds(10));
                    });
                });
    }
}
