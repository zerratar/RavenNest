using System.Net;

namespace RavenNest.LoadTest;

/// <summary>
/// The tool creates users, characters and items and pushes a lot of traffic. It must only ever hit a
/// development server on this machine, unless told otherwise in so many words.
/// </summary>
public static class Safety
{
    public static bool IsLocalHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        host = host.Trim().Trim('[', ']');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // only literal addresses: a name could resolve anywhere, today or tomorrow
        return IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip);
    }

    /// <summary>Null when it is fine to go ahead, otherwise why not.</summary>
    public static string? Check(Options o)
    {
        if (o.AllowNonLocal)
        {
            return null;
        }

        if (!IsLocalHost(o.BaseUrl.Host))
        {
            return "Refusing to run against '" + o.BaseUrl.Host + "': only localhost, 127.0.0.1 or ::1 are allowed. "
                + "If this really is a test server, add --i-know-this-is-not-production.";
        }

        if (!IsLocalHost(o.EffectiveTcpHost))
        {
            return "Refusing to connect the sockets to '" + o.EffectiveTcpHost + "': only localhost, 127.0.0.1 or ::1 are allowed. "
                + "If this really is a test server, add --i-know-this-is-not-production.";
        }

        return null;
    }
}
