namespace RavenNest.BusinessLogic.Data
{
    public static class FolderPaths
    {
        public const string GeneratedData = "../generated-data";

        // all paths below are acombined with GeneratedDataFolder
        // so the real paths are "GeneratedData/..."

        public const string OpenAILogs = "openai-logs";

        public const string PatreonRequestData = "patreon";
        public const string BinaryCache = "cache";

        public const string Restorepoints = "restorepoints";
        public const string Backups = "backups";

        public const string SessionPlayers = "session-players";

        public const string BadInventory = "bad-inventory";
    }
}
