namespace RavenNest.HeadlessClient
{
    public class AppSettings
    {
        public AppSettings(
            string username,
            string password,
            string gameKey,
            string gameVersion,
            string backupFolder)
        {
            Username = username;
            Password = password;
            GameKey = gameKey;
            GameVersion = gameVersion;
            BackupFolder = backupFolder;
        }

        public string Username { get; }
        public string Password { get; }
        public string GameKey { get; }
        public string GameVersion { get; }
        public string BackupFolder { get; }
    }
}
