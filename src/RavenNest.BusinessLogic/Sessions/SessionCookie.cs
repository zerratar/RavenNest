using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace RavenNest
{
    public class SessionCookie
    {
        public const string SessionCookieName = "__ravenSession";

        public static readonly TimeSpan SessionTimeout = TimeSpan.FromDays(7);

        public static string GetSessionId(IReadOnlyDictionary<string, string> headers)
        {
            if (headers.TryGetValue(SessionCookieName, out var sid) && !string.IsNullOrEmpty(sid))
            {
                return sid;
            }
            return Guid.NewGuid().ToString();
        }

        public static string GetSessionId(HttpContext context)
        {
            if (context == null) return Guid.NewGuid().ToString();
            if (context.Request.Headers.ContainsKey(SessionCookieName) &&
                context.Request.Headers.TryGetValue(SessionCookieName, out var sid) &&
                !string.IsNullOrEmpty(sid))
            {
                return sid;
            }

            if (!context.Request.Cookies.TryGetValue(SessionCookieName, out var id))
            {
                try
                {
                    return AppendSessionToken(context, sid);
                }
                catch { }
            }
            return id;
        }

        public static async Task SessionCookieMiddleware(HttpContext context, Func<Task> next)
        {
            if (!context.Request.Cookies.ContainsKey(SessionCookieName))
            {
                var id = context.GetSessionId();
                AppendSessionToken(context, id);
            }

            await next.Invoke();
        }

        private static string AppendSessionToken(HttpContext context, string sessionId = null)
        {
            var id = sessionId != null ? sessionId : Guid.NewGuid().ToString();
            if (context != null)
            {
                context.Response.Cookies.Append(SessionCookieName, id, new CookieOptions
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

        public static string GetSessionId(this IReadOnlyDictionary<string, string> headers)
        {
            return SessionCookie.GetSessionId(headers);
        }
    }
}
