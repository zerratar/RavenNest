using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace RavenNest
{
    public class SessionCookie
    {
        const string sessionCookie = "__ravenSession";
        public static string GetSessionId(HttpContext context)
        {
            context.Request.Cookies.TryGetValue(sessionCookie, out var id);
            return id;
        }

        public static async Task SessionCookieMiddleware(HttpContext context, Func<Task> next)
        {
            if (!context.Request.Cookies.ContainsKey(sessionCookie))
            {
                context.Response.Cookies.Append(sessionCookie, Guid.NewGuid().ToString());
            }

            await next.Invoke();
        }
    }

    public static class SessionCookieExt
    {
        public static void AddSessionCookies(this IApplicationBuilder app)
        {
            app.Use(SessionCookie.SessionCookieMiddleware);
        }

        public static string GetSessionId(this HttpContext ctx)
        {
            return SessionCookie.GetSessionId(ctx);
        }
    }
}
