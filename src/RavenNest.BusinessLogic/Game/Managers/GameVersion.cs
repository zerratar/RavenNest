using System;
using System.Runtime.CompilerServices;
using RavenNest.BusinessLogic.Data;

namespace RavenNest.BusinessLogic.Game
{
    public static class GameVersion
    {
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
        public static Version GetClientVersion(this IGameData gameData)
        {
            if (gameData.Client == null || !TryParse(gameData.Client.ClientVersion, out var version))
                return new Version();
            return version;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsExpectedVersion(this DataModels.GameSession session, IGameData gameData)
        {
            return IsExpectedVersion(gameData, session);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsExpectedVersion(this IGameData gameData, DataModels.GameSession session)
        {
            var sessionState = gameData.GetSessionState(session.Id);
            return IsExpectedVersion(gameData, sessionState);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsExpectedVersion(this IGameData gameData, DataModels.SessionState sessionState)
        {
            if (sessionState == null) return false;
            return IsExpectedVersion(gameData, sessionState.ClientVersion);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsExpectedVersion(this IGameData gameData, string versionString)
        {
            if (string.IsNullOrEmpty(versionString))
                return false;

            if (TryParse(versionString, out var version))
                return IsExpectedVersion(gameData, version);

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsExpectedVersion(this IGameData gameData, Version version)
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
