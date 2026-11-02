using Microsoft.AspNetCore.Hosting;
using System.Globalization;

namespace RavenNest.Blazor.Extensions
{
    public static class WebHostEnvironmentExtensions
    {
        /// <summary>
        /// A wwwroot file's address with its last change in it ("./js/x.js?v=..."), for scripts a page imports from
        /// code. Those imports come after the page has loaded, so a hard refresh does not reach them and a browser
        /// kept running the old script after an update.
        /// </summary>
        public static string Versioned(this IWebHostEnvironment environment, string path)
        {
            var file = environment.WebRootFileProvider.GetFileInfo(path);
            var version = file.Exists ? file.LastModified.UtcTicks.ToString(CultureInfo.InvariantCulture) : "0";
            return "./" + path + "?v=" + version;
        }
    }
}
