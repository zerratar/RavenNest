using System.IO;

namespace RavenNest.BusinessLogic.Data
{
    public static class FolderPaths
    {
#if DEBUG
        public const string DataFolder = "G:\\Ravenfall\\Dev-Data\\";
#else
        public const string DataFolder = "G:\\Ravenfall\\Data";
#endif
        public readonly static string PublishPath = "G:\\Ravenfall\\Projects\\RavenNest\\Publish";

        public readonly static string GeneratedDataPath = Path.Combine(DataFolder, "generated-data");
        public readonly static string UserSettingsPath = Path.Combine(DataFolder, "user-settings");
        public readonly static string LogsPath = Path.Combine(DataFolder, "logs");

        // all paths below are acombined with GeneratedDataFolder
        // so the real paths are "GeneratedData/..."

        public const string OpenAILogs = "openai-logs";

        public const string PatreonRequestData = "patreon";
        public const string BinaryCache = "cache";

        public const string Restorepoints = "restorepoints";
        public const string Backups = "backups";
        public const string Merge = "merge";
        public const string SessionPlayers = "session-players";

        public const string BadInventory = "bad-inventory";

    }
}
