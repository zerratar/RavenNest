namespace RavenNest.HeadlessClient
{
    public class AppSettings
    {
        public AppSettings(string username, string password, string gameKey, string gameVersion)
        {
            Username = username;
            Password = password;
            GameKey = gameKey;
            GameVersion = gameVersion;
        }

        public string Username { get; }
        public string Password { get; }
        public string GameKey { get; }
        public string GameVersion { get; }
    }
}
