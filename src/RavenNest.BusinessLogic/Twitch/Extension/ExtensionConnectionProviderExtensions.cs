using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Twitch.Extension
{
    public static class ExtensionConnectionProviderExtensions
    {
        public static Task<bool> BroadcastAsync<T>(this ITwitchExtensionConnectionProvider connectionProvider, T update)
        {
            return connectionProvider.ForAllConnectionsAsync(x => x.SendAsync(update));
        }

        private static async Task<bool> ForAllConnectionsAsync(
            this ITwitchExtensionConnectionProvider connectionProvider,
            Func<IExtensionConnection, Task> action)
        {
            var connection = connectionProvider.GetAll();
            if (connection == null) return false;
            await connection.ForEachAsync(action);
            return true;
        }

        public static Task ForEachAsync<T>(this IEnumerable<T> source, Func<T, Task> action)
        {
            return Task.WhenAll(source.Select(action));
        }
    }
}
