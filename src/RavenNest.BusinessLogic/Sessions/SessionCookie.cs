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
            if (headers.TryGetValue(SessionCookieName, out var sid) && !string.IsNullOrEmpty(sid) && sid != "null")
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
                !string.IsNullOrEmpty(sid) && sid != "null")
            {
                return sid;
            }

            if (!context.Request.Cookies.TryGetValue(SessionCookieName, out var id))
            {
                if (TryAppendSessionToken(context, sid, out id) && id != "null")
                {
                    return id;
                }
            }

            if (string.IsNullOrEmpty(id) || id == "null")
            {
                if (TryAppendSessionToken(context, null, out id))
                {
                    return id;
                }
            }

            return id;
        }

        public static async Task SessionCookieMiddleware(HttpContext context, Func<Task> next)
        {
            if (!context.Request.Cookies.ContainsKey(SessionCookieName))
            {
                var id = context.GetSessionId();
                TryAppendSessionToken(context, id, out _);
            }

            await next.Invoke();
        }

        private static bool TryAppendSessionToken(HttpContext context, string sessionId, out string newSessionId)
        {
            newSessionId = sessionId != null ? sessionId : Guid.NewGuid().ToString();
            try
            {
                if (context != null)
                {
                    context.Response.Cookies.Append(SessionCookieName, newSessionId, new CookieOptions
                    {
                        Expires = DateTime.UtcNow.Add(SessionTimeout),
                        MaxAge = SessionTimeout
                    });
                }
                return true;
            }
            catch
            {
                return false;
            }
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
