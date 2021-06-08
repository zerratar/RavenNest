using RavenNest.SDK;
using System;
using System.Threading.Tasks;

namespace RavenNest.HeadlessClient
{
    public class GameClient : IGameClient
    {
        private readonly AppSettings settings;
        private readonly ILogger logger;
        private readonly IGameManager gameManager;
        private readonly IRavenNestClient ravennest;
        private int waitEventCount;
        private TimeSpan waitTimeout;
        private DateTime waitStart;
        private int receivedEventCount;

        public GameClient(
            AppSettings settings,
            ILogger logger,
            IGameManager gameManager,
            IRavenNestClient ravennest)
        {
            this.settings = settings;
            this.logger = logger;
            this.gameManager = gameManager;
            this.ravennest = ravennest;
        }

        public async Task<bool> BeginGameSessionAsync()
        {
            if (string.IsNullOrEmpty(this.settings.GameKey))
            {
                logger.Error("settings.json does not contain a gamekey. Please update it and run the application again.");
                return false;
            }

            var result = await ravennest.StartSessionAsync(this.settings.GameVersion, this.settings.GameKey, false);
            if (!result)
            {
                logger.Error("Bad game version or game key. Please update the settings.json and run the application again.");
                return false;
            }

            logger.WriteLine("Game Session Started");
            return result;
        }

        public async Task<bool> AuthenticateAsync()
        {
            if (string.IsNullOrEmpty(this.settings.Username) || string.IsNullOrEmpty(this.settings.Password))
            {
                logger.Error("settings.json does not contain a username or password. Please update it and run the application again.");
                return false;
            }

            var loginResult = await ravennest.LoginAsync(this.settings.Username, this.settings.Password);
            if (!loginResult)
            {
                logger.Error("Username or password provided in the settings.json is invalid. Please update it and run the application again.");
                return false;
            }

            logger.WriteLine("Login to RavenNest OK");
            return loginResult;
        }

        public async Task DownloadBackupAsync()
        {
            try
            {
                logger.Write("Downloading backup from server... ");

                var targetFolder = settings.BackupFolder ?? "backups";
                var bytes = await ravennest.Admin.DownloadBackupAsync();
                if (bytes == null || bytes.Length == 0)
                {
                    logger.WriteLine("Failed.");
                    return;
                }

                logger.WriteLine("Completed.");

                if (!System.IO.Directory.Exists(targetFolder))
                {
                    System.IO.Directory.CreateDirectory(targetFolder);
                }
                
                var outputFile = System.IO.Path.Combine(targetFolder, "data-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".zip");
                await System.IO.File.WriteAllBytesAsync(outputFile, bytes);
            }
            catch (Exception exc)
            {
                logger.Error("ERR: " + exc);
            }
        }

        public void Dispose()
        {
            if (this.ravennest.SessionStarted)
            {
                this.ravennest.EndSessionAsync();
                this.logger.WriteLine("Client and session terminated.");
            }
            else
            {
                this.logger.Error("Client terminated without a game session.");
            }
        }

        public async Task<bool> WaitForGameEventsAsync(int eventCount, TimeSpan timeout)
        {
            this.waitEventCount = eventCount;
            this.waitTimeout = timeout;

            if (this.waitStart == DateTime.MinValue)
            {
                this.waitStart = DateTime.UtcNow;
            }

            if (DateTime.UtcNow - this.waitStart >= timeout)
            {
                return false;
            }

            // ensure we are connected to the websocket stream
            await ravennest.UpdateAsync();

            if (this.gameManager.GameEventCount >= eventCount)
            {
                return false;
            }

            return true;
        }

    }
}
