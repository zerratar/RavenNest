using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace RavenNest
{
    public class SessionCookie
    {
        private const string sessionCookie = "__ravenSession";

        public static readonly TimeSpan SessionTimeout = TimeSpan.FromDays(7);

        public static string GetSessionId(HttpContext context)
        {
            if (context == null) return Guid.NewGuid().ToString();
            if (!context.Request.Cookies.TryGetValue(sessionCookie, out var id))
            {
                try
                {
                    return AppendSessionToken(context);
                }
                catch { }
            }
            return id;
        }

        public static async Task SessionCookieMiddleware(HttpContext context, Func<Task> next)
        {
            if (!context.Request.Cookies.ContainsKey(sessionCookie))
            {
                AppendSessionToken(context);
            }

            await next.Invoke();
        }

        private static string AppendSessionToken(HttpContext context)
        {
            var id = Guid.NewGuid().ToString();
            if (context != null)
            {
                context.Response.Cookies.Append(sessionCookie, id, new CookieOptions
                {
                    Expires = DateTime.UtcNow.Add(SessionTimeout),
                    MaxAge = SessionTimeout
                });
            }
            return id;
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
