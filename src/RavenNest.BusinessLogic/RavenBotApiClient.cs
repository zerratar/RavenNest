using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic
{
    public class RavenBotApiClient : IRavenBotApiClient
    {

        const string SettingsDirectory = "../user-settings/";


        //#if DEBUG
        //        private const string host = "127.0.0.1";
        //#else
        private string[] hostNames = { "ravenbot.ravenfall.stream", "127.0.0.1" };
#if DEBUG
        private int currentHostIndex = 1;
#else
        private int currentHostIndex = 0;
#endif

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

        public async Task SendUserSettingAsync(string userId, string key, string value)
        {
            await SendAsync(currentHostIndex, "usersettings", userId, key, value);
        }

        public async Task SendUserSettingsAsync(string userId, Dictionary<string, string> settings)
        {
            // lets just overwrite the actual json file.
            // the bot will realize it has changed and will reload the file.
            try
            {
                var targetFile = System.IO.Path.Combine(SettingsDirectory, userId + ".json");
                var dir = System.IO.Path.GetDirectoryName(targetFile);
                if (System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                System.IO.File.WriteAllText(targetFile, Newtonsoft.Json.JsonConvert.SerializeObject(settings));
            }
            catch
            {
                try
                {
                    using (var req = RavenBotRequest.Create(BuildRequestUri(currentHostIndex, "usersettings"), logger))
                    {
                        foreach (var v in settings)
                        {
                            await req.SendAsync(userId, v.Key, v.Value);
                            await Task.Delay(100);
                        }
                    }
                }
                catch (Exception exc)
                {
                    logger.LogError(exc.ToString());
                }
            }
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
                    if (await req.SendAsync(args))
                    {
                        //logger.LogError("[Not an error] " + req.ToString() + " - sent successfully.");
                        return;
                    }

                    // RavenBot is either down or bad hostname.
                    // to ensure it has nothing to do with the hostname,
                    // switch to the next available one.
                    // we then want to re-schedule this call
                    currentHostIndex = (currentHostIndex + 1) % hostNames.Length;
                    RescheduleSend(currentHostIndex, method, args);
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
