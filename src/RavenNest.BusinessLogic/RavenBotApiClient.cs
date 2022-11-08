using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic
{
    public class RavenBotApiClient : IRavenBotApiClient
    {
        //#if DEBUG
        //        private const string host = "127.0.0.1";
        //#else
        private string[] hostNames = { "ravenbot.ravenfall.stream", "127.0.0.1" };
        private int currentHostIndex = 0;
        //#endif

        private const int RETRY_INTERVAL = 3000;
        private const int PORT = 6767;

        private readonly ILogger<RavenBotApiClient> logger;
        private readonly IKernel kernel;
        private readonly ConcurrentDictionary<string, ITimeoutHandle> retries = new();

        public RavenBotApiClient(ILogger<RavenBotApiClient> logger, IKernel kernel)
        {
            this.logger = logger;
            this.kernel = kernel;
        }

        public async Task SendPubSubAccessTokenAsync(string id, string login, string accessToken)
        {
            await SendAsync(currentHostIndex, "pubsub", id, login, accessToken);
        }

        public async Task SendUserRoleAsync(string userId, string userName, string role)
        {
            await SendAsync(currentHostIndex, "userrole", userId, userName, role);
        }

        private async Task SendAsync(int hostIndex, string method, params string[] args)
        {
            try
            {
                using (var req = RavenBotRequest.Create(BuildRequestUri(hostIndex, method), logger))
                {
                    if (!await req.SendAsync(args))
                    {
                        // RavenBot is either down or bad hostname.
                        // to ensure it has nothing to do with the hostname,
                        // switch to the next available one.
                        // we then want to re-schedule this call
                        currentHostIndex = (currentHostIndex + 1) % hostNames.Length;
                        RescheduleSend(currentHostIndex, method, args);
                    }
                }
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
            }
        }

        private void RescheduleSend(int hostIndex, string method, params string[] args)
        {
            string key = GetRetryKey(method, args);
            if (retries.ContainsKey(key))
            {
                // do not retry the same one again.
                return;
            }

            var index = hostIndex;
            var retryHandle = kernel.SetTimeout(async () =>
            {
                await SendAsync(index, method, args);
                retries.TryRemove(key, out _);
            }, RETRY_INTERVAL);

            retries[key] = retryHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GetRetryKey(string method, string[] args)
        {
            if (args.Length > 1)
            {
                return method + "+" + args[0] + "+" + args[1];
            }

            if (args.Length > 0)
            {
                return method + "+" + args[0];
            }

            return method;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string BuildRequestUri(int hostIndex, string method)
        {
            return $"{hostNames[hostIndex]}:{PORT}/{method}";
        }

    }
}
