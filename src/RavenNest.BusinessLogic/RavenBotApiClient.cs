using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
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
        private readonly string[] hostNames = { "ravenbot.ravenfall.stream", "127.0.0.1" };
#if DEBUG
        private int currentHostIndex = 1;
#else
        private int currentHostIndex = 0;
#endif

        //#endif

        private const int RETRY_INTERVAL = 3000;
        private const int PORT = 6767;
        private readonly GameData gameData;
        private readonly ILogger<RavenBotApiClient> logger;
        private readonly IKernel kernel;
        private readonly ConcurrentDictionary<string, ITimeoutHandle> retries = new();

        public RavenBotApiClient(GameData gameData, ILogger<RavenBotApiClient> logger, IKernel kernel)
        {
            this.gameData = gameData;
            this.logger = logger;
            this.kernel = kernel;
        }

        public async Task UpdateUserSettingsAsync(Guid userId)
        {
            // lets just overwrite the actual json file.
            // the bot will realize it has changed and will reload the file.
            try
            {
                var targetFile = System.IO.Path.Combine(FolderPaths.UserSettingsPath, userId + ".json");
                var dir = System.IO.Path.GetDirectoryName(targetFile);

                var settings = gameData.GetUserSettings(userId);
                if (settings == null)
                {
                    return; // we don't have anything to save.
                }

                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(settings);

                var lockFile = targetFile + ".lock";

                const int maxRetries = 5;
                for (int i = 0; i < maxRetries; i++)
                {
                    if (System.IO.File.Exists(lockFile))
                    {
                        await Task.Delay(100);
                    }

                    try
                    {
                        System.IO.File.WriteAllText(targetFile, json);
                        break; // success, break out of the loop
                    }
                    catch (System.IO.IOException)
                    {
                        if (i == maxRetries - 1)
                            throw; // last attempt, rethrow exception
                        await Task.Delay(100); // wait for 100 ms before retrying
                    }
                }
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
            }

            // Deliberately no network push here. This runs on every successful player join, so at a
            // thousand players a stream that would be a couple of thousand extra requests to the bot
            // for names that did not change. PushUserNamesAsync is called instead by the paths that
            // actually detect a rename.
        }

        /// <summary>
        /// Sends the name fields to the bot.
        /// </summary>
        /// <remarks>
        /// Call only when a name has actually changed. The file written by
        /// <see cref="UpdateUserSettingsAsync"/> only reaches a bot on the same machine, which is
        /// not the normal deployment, so without this a rename never reached the bot at all.
        ///
        /// <para>
        /// Only the names are sent rather than the whole settings object, because these are the
        /// values the bot uses to decide which channel a session belongs to and each setting costs
        /// one request.
        /// </para>
        /// </remarks>
        public async Task PushUserNamesAsync(Guid userId)
        {
            try
            {
                var settings = gameData.GetUserSettings(userId);
                if (settings == null)
                {
                    return;
                }

                if (settings.TryGetValue("ravenfall_name", out var ravenfallName) && ravenfallName != null)
                {
                    await SendUserSettingAsync(userId, "ravenfall_name", ravenfallName.ToString());
                }

                if (settings.TryGetValue("twitch_name", out var twitchName) && twitchName != null)
                {
                    await SendUserSettingAsync(userId, "twitch_name", twitchName.ToString());
                }
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
            }
        }

        public void UpdateUserSettings(Guid userId)
        {
            // lets just overwrite the actual json file.
            // the bot will realize it has changed and will reload the file.
            try
            {
                var targetFile = System.IO.Path.Combine(FolderPaths.UserSettingsPath, userId + ".json");
                var dir = System.IO.Path.GetDirectoryName(targetFile);

                var settings = gameData.GetUserSettings(userId);
                if (settings == null)
                {
                    return; // we don't have anything to save.
                }

                // The check was inverted here: the directory was only created when it already
                // existed, so the write below threw on a fresh install and the settings were lost.
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(settings);
                System.IO.File.WriteAllText(targetFile, json);
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
            }
        }

        public async Task SendUserSettingAsync(Guid userId, string key, string value)
        {
            if (userId == Guid.Empty || string.IsNullOrEmpty(key))
            {
                return;
            }

            // "usersettings" resolves to UserSettingsPacketHandler on the bot, which has always been
            // there but was never called: settings were only ever written to a file. That works when
            // both run on the same machine and silently does nothing otherwise, which is the normal
            // deployment.
            await SendAsync(currentHostIndex, "usersettings", userId.ToString(), key, value ?? string.Empty);
        }

        public async Task SendTwitchPubSubAccessTokenAsync(string id, string login, string accessToken)
        {
            await SendAsync(currentHostIndex, "pubsub", id, login, accessToken);
        }

        public async Task SendUserRoleAsync(string userId, string platform, string userName, string role)
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
