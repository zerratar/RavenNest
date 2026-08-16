using System;
using System.Runtime.CompilerServices;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// Applies a platform username change to every copy of the name we keep.
    /// </summary>
    /// <remarks>
    /// The name lives in four places: <see cref="User.UserName"/>, <see cref="User.DisplayName"/>,
    /// <see cref="UserAccess.PlatformUsername"/> and <see cref="Character.Name"/>. Before this
    /// existed, each refresh path wrote a different subset of them, so which fields healed after a
    /// rename depended on whether the person logged in on the website, joined a game, or both. That
    /// left accounts in mixed states that had to be repaired by hand in the database.
    ///
    /// <para>
    /// Everything here is a plain in-memory mutation. The entities are change tracked and written
    /// back by the batched upserts, so a rename applied through this takes effect immediately for
    /// every connected client without restarting anything.
    /// </para>
    /// </remarks>
    public static class UserNameSync
    {
        /// <summary>
        /// Brings all stored copies of the name in line with what the platform currently reports.
        /// </summary>
        /// <param name="gameData">Data layer holding the entities to update.</param>
        /// <param name="user">The Ravenfall account.</param>
        /// <param name="platform">Platform the name came from, for example "twitch".</param>
        /// <param name="platformId">
        /// Stable platform id. Used to fill in or correct the access row; may be null when the
        /// caller only knows the name.
        /// </param>
        /// <param name="platformUserName">Current login name on the platform.</param>
        /// <param name="platformDisplayName">
        /// Current display name on the platform. Falls back to the login name when not supplied.
        /// </param>
        /// <returns>True when something actually changed.</returns>
        public static bool Apply(
            GameData gameData,
            User user,
            string platform,
            string platformId,
            string platformUserName,
            string platformDisplayName = null)
        {
            if (gameData == null || user == null || string.IsNullOrEmpty(platformUserName))
            {
                return false;
            }

            platformUserName = platformUserName.Trim();
            if (platformUserName.Length == 0)
            {
                return false;
            }

            var changed = false;

            // Accounts that exist on more than one platform carry a "name@platform" suffix in
            // UserName. Only the part before the suffix is the platform name, so the suffix is put
            // back rather than being dropped and turning the account into a plain "name".
            var suffix = GetPlatformSuffix(user.UserName);
            var newUserName = platformUserName + suffix;

            if (!string.Equals(user.UserName, newUserName, StringComparison.Ordinal))
            {
                user.UserName = newUserName;
                changed = true;
            }

            var newDisplayName = Utility.SanitizeUserName(
                string.IsNullOrEmpty(platformDisplayName) ? platformUserName : platformDisplayName,
                platformUserName);

            if (!string.IsNullOrEmpty(newDisplayName) &&
                !string.Equals(user.DisplayName, newDisplayName, StringComparison.Ordinal))
            {
                user.DisplayName = newDisplayName;
                changed = true;
            }

            if (!string.IsNullOrEmpty(platform))
            {
                var access = gameData.GetUserAccess(user.Id, platform);
                if (access != null)
                {
                    if (!string.Equals(access.PlatformUsername, platformUserName, StringComparison.Ordinal))
                    {
                        access.PlatformUsername = platformUserName;
                        access.Updated = DateTime.UtcNow;
                        changed = true;
                    }

                    // The id is what we matched on, so it is normally already correct. It is only
                    // written when the row is missing one, which happens on rows created before the
                    // platform id was recorded.
                    if (!string.IsNullOrEmpty(platformId) && string.IsNullOrEmpty(access.PlatformId))
                    {
                        access.PlatformId = platformId;
                        access.Updated = DateTime.UtcNow;
                        changed = true;
                    }
                }
                else if (!string.IsNullOrEmpty(platformId))
                {
                    gameData.Add(new UserAccess
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        Platform = platform,
                        PlatformId = platformId,
                        PlatformUsername = platformUserName,
                        Created = DateTime.UtcNow,
                        Updated = DateTime.UtcNow
                    });
                    changed = true;
                }
            }

            // Character names track the account name, which is what the join path already enforces
            // every time a player joins. Leaving them behind is what made the bot and the game
            // disagree about who a player was after a rename.
            var characters = gameData.GetCharactersByUserId(user.Id);
            if (characters != null)
            {
                foreach (var character in characters)
                {
                    if (character == null)
                    {
                        continue;
                    }

                    if (!string.Equals(character.Name, platformUserName, StringComparison.Ordinal))
                    {
                        character.Name = platformUserName;
                        changed = true;
                    }
                }
            }

            return changed;
        }

        /// <summary>
        /// Returns the "@platform" part of a Ravenfall username, or an empty string when there is
        /// none. Accounts that only exist on one platform have no suffix.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetPlatformSuffix(string userName)
        {
            if (string.IsNullOrEmpty(userName))
            {
                return string.Empty;
            }

            var index = userName.IndexOf('@');
            return index < 0 ? string.Empty : userName.Substring(index).Trim();
        }
    }
}
