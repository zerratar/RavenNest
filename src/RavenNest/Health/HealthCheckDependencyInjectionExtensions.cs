using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Newtonsoft.Json;
using RavenNest.BusinessLogic;

namespace RavenNest.Health
{
    public static class HealthCheckDependencyInjectionExtensions
    {
        public static IServiceCollection AddRavenNestHealthChecks(this IServiceCollection services, IConfiguration configuration)
        {
            var settings = configuration.Get<AppSettings>();
            services.AddHealthChecks()
                    .AddSqlServer(settings.DbConnectionString, name: "SqlServer")
                    .AddCheck<GameServerHealthCheck>(nameof(GameServerHealthCheck));
            
            return services;
        }

        public static IEndpointRouteBuilder MapRavenNestHealthChecks(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapHealthChecks("/health", new HealthCheckOptions()
            {
                ResponseWriter = WriteResponse
            });
            return endpoints;
        }

        private static Task WriteResponse(HttpContext context, HealthReport result)
        {
            context.Response.ContentType = "application/json; charset=utf-8";

            var responseObj = new HealthResponse
            {
                Status = result.Status.ToString(),
                Results = result.Entries.ToDictionary(e => e.Key, e => 
                    new HealthResult
                    {
                        Status = e.Value.Status.ToString(),
                        Description = e.Value.Description,
                        Data = e.Value.Data
                    })
            };

            var response = JsonConvert.SerializeObject(responseObj, Formatting.Indented);
            return context.Response.WriteAsync(response);
        }
    }
}
