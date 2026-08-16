using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;

namespace RavenNest.BusinessLogic.Settings
{
    public interface IServerSettingsProvider
    {
        IReadOnlyList<ServerSettingDefinition> Definitions { get; }

        ServerSettingDefinition Find(string key);

        string GetString(string key);

        long GetNumber(string key, long fallback = 0);

        double GetDecimal(string key, double fallback = 0);

        bool GetToggle(string key, bool fallback = false);

        bool IsSet(string key);

        ServerSettingSource GetSource(string key);

        /// <summary>
        ///     Enough of a secret to recognise it, and not enough to use it.
        /// </summary>
        string Describe(string key);

        void Set(string key, string value, string changedBy);
    }

    /// <summary>
    ///     Reads and writes the server's own settings, so that turning something on does not mean
    ///     editing a file on the host and restarting.
    /// </summary>
    /// <remarks>
    ///     Three places are consulted, in order: the database, the configuration file, then the
    ///     built in default. The database wins because it is the one a person can change without a
    ///     deploy; the configuration file still works untouched, which matters because that is where
    ///     everything lives today and none of it should have to move at once.
    ///
    ///     <para>
    ///     Secrets go in the same table as everything else. That is not encryption and is not
    ///     claimed to be: anybody who can read the database can read the key. What it does buy is
    ///     that the key stops being copied into a file on disk, into a backup of that file, and into
    ///     whatever is watching the deploy. The value is never rendered back to the page and never
    ///     written to a log.
    ///     </para>
    ///
    ///     <para>
    ///     Lookups scan the loaded settings rather than caching. There are a few dozen rows, they
    ///     are already in memory, and a cache would need invalidating the moment somebody changes
    ///     one, which is exactly when being wrong would be least obvious.
    ///     </para>
    /// </remarks>
    public class ServerSettingsProvider : IServerSettingsProvider
    {
        private readonly GameData gameData;
        private readonly IConfiguration configuration;
        private readonly ILogger<ServerSettingsProvider> logger;

        public ServerSettingsProvider(
            GameData gameData,
            IConfiguration configuration,
            ILogger<ServerSettingsProvider> logger)
        {
            this.gameData = gameData;
            this.configuration = configuration;
            this.logger = logger;
        }

        public IReadOnlyList<ServerSettingDefinition> Definitions => ServerSettingsRegistry.All;

        public ServerSettingDefinition Find(string key) =>
            ServerSettingsRegistry.All.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

        public string GetString(string key)
        {
            var stored = StoredValue(key);
            if (stored != null) return stored;

            var configured = configuration?[key];
            if (!string.IsNullOrWhiteSpace(configured)) return configured;

            return Find(key)?.DefaultValue;
        }

        public long GetNumber(string key, long fallback = 0)
        {
            var value = GetString(key);
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;
        }

        public double GetDecimal(string key, double fallback = 0)
        {
            var value = GetString(key);
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;
        }

        public bool GetToggle(string key, bool fallback = false)
        {
            var value = GetString(key);
            if (string.IsNullOrWhiteSpace(value)) return fallback;

            // "1" and "yes" turn up in configuration files often enough to be worth accepting, and a
            // setting that silently reads as off because somebody wrote yes is a bad afternoon.
            if (bool.TryParse(value, out var parsed)) return parsed;
            if (value.Equals("1", StringComparison.Ordinal)) return true;
            if (value.Equals("0", StringComparison.Ordinal)) return false;
            if (value.Equals("yes", StringComparison.OrdinalIgnoreCase)) return true;
            if (value.Equals("no", StringComparison.OrdinalIgnoreCase)) return false;

            return fallback;
        }

        public bool IsSet(string key) => !string.IsNullOrWhiteSpace(GetString(key));

        public ServerSettingSource GetSource(string key)
        {
            if (StoredValue(key) != null) return ServerSettingSource.Database;
            if (!string.IsNullOrWhiteSpace(configuration?[key])) return ServerSettingSource.Configuration;
            return ServerSettingSource.Default;
        }

        /// <summary>
        ///     What to show on the page. A secret comes back as its last four characters and nothing
        ///     else, which is enough to tell two keys apart and useless to anybody looking over a
        ///     shoulder or watching a screen share.
        /// </summary>
        public string Describe(string key)
        {
            var value = GetString(key);
            if (string.IsNullOrWhiteSpace(value)) return null;

            var definition = Find(key);
            if (definition == null || !definition.IsSecret) return value;

            var tail = value.Length <= 4 ? value : value.Substring(value.Length - 4);
            return "ends in " + tail;
        }

        public void Set(string key, string value, string changedBy)
        {
            var definition = Find(key);
            if (definition == null)
            {
                // Only settings the panel knows about. Anything else is a typo, and storing it would
                // look like it worked.
                logger.LogWarning("Refused to store the unknown server setting '" + key + "'.");
                return;
            }

            var settings = gameData.GetOrCreateServerSettings(definition.Key);

            // Blank clears it, which drops back to the configuration file or the default rather than
            // storing an empty string that reads as "set to nothing".
            settings.Value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

            // The key name and who changed it, never the value. This line ends up in a log file that
            // is read by more people, and in more places, than the database is.
            logger.LogWarning(
                "Server setting '" + definition.Key + "' was " +
                (settings.Value == null ? "cleared" : "changed") +
                " by " + (string.IsNullOrWhiteSpace(changedBy) ? "an administrator" : changedBy) + ".");
        }

        /// <summary>
        ///     The stored value, or null when there is no row or the row is empty. Empty is treated
        ///     as absent so that clearing a setting falls back rather than overriding with nothing.
        /// </summary>
        private string StoredValue(string key)
        {
            var row = gameData.GetServerSettings(key);
            return string.IsNullOrWhiteSpace(row?.Value) ? null : row.Value;
        }
    }
}
