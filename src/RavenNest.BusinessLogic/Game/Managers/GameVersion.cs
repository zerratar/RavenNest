using System;
using System.Runtime.CompilerServices;
using RavenNest.BusinessLogic.Data;

namespace RavenNest.BusinessLogic.Game
{
    public static class GameUpdates
    {
        public const string RequiresExpTransformation = "0.8.8.4a";

        public const string DisableExpSave_LessThanOrEquals = "0.8.8.4a";

        private static Version[] requiredUpdates = new Version[]
        {
            GameVersion.Parse("0.8.8.5a"),
            GameVersion.Parse("0.9.0.0a"),
        };

        public static bool IsRequiredUpdate(string update)
        {
            if (!GameVersion.TryParse(update, out var v))
            {
                return false;
            }

            foreach (var ver in requiredUpdates)
            {
                if (ver.Equals(v))
                    return true;
            }

            return false;
        }
    }

    public static class GameVersion
    {
        public static Version Parse(string input)
        {
            TryParse(input, out var value);
            return value ?? new Version();
        }

        public static bool TryParse(string input, out Version version)
        {
            if (string.IsNullOrEmpty(input))
            {
                version = new Version();
                return false;
            }

            var versionString = input.ToLower().Replace("a-alpha", "").Replace("v", "").Replace("a", "").Replace("b", "");
            return System.Version.TryParse(versionString, out version);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Version GetClientVersion(this GameData gameData)
        {
            if (gameData.Client == null || !TryParse(gameData.Client.ClientVersion, out var version))
                return new Version();
            return version;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsExpectedVersion(this DataModels.GameSession session, GameData gameData)
        {
            return IsExpectedVersion(gameData, session);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsExpectedVersion(this GameData gameData, DataModels.GameSession session)
        {
            var sessionState = gameData.GetSessionState(session.Id);
            return IsExpectedVersion(gameData, sessionState);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsExpectedVersion(this GameData gameData, DataModels.SessionState sessionState)
        {
            if (sessionState == null) return false;
            return IsExpectedVersion(gameData, sessionState.ClientVersion);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsExpectedVersion(this GameData gameData, string versionString)
        {
            if (string.IsNullOrEmpty(versionString))
                return false;

            if (TryParse(versionString, out var version))
                return IsExpectedVersion(gameData, version);

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsExpectedVersion(this GameData gameData, Version version)
        {
            return version >= gameData.GetClientVersion();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLessThanOrEquals(string version, string comparison)
        {
            if (!TryParse(version, out var src))
                return false;
            if (!TryParse(comparison, out var dst))
                return false;
            return src <= dst;
        }
    }
}
