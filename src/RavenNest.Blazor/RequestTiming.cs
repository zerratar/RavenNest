using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RavenNest
{

    public class RequestTiming
    {
        public static async Task TimingMiddleware(HttpContext context, Func<Task> next)
        {
            var logger = context.RequestServices.GetService<ILogger<RequestTiming>>();
            var sw = new Stopwatch();
            sw.Start();
            try
            {
                await next.Invoke();
            }
            finally
            {
                sw.Stop();

                if (sw.ElapsedMilliseconds > 1000)
                    logger.LogWarning($"Very long request finished after {sw.ElapsedMilliseconds}ms ({context.Request.Path})");
                else
                    logger.LogInformation($"Request finished after {sw.ElapsedMilliseconds}ms ({context.Request.Path})");

            }
        }
    }

    public static class RequestTimingExt
    {
        public static void AddRequestTiming(this IApplicationBuilder app)
        {
            app.Use(RequestTiming.TimingMiddleware);
        }
    }
}
