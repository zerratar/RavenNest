using System.Threading.Tasks;

namespace RavenNest.BusinessLogic
{
    public interface IRavenBotApiClient
    {
        Task UpdateUserSettingsAsync(System.Guid userId);

        /// <summary>
        /// Sends a single user setting to the bot over the network.
        /// </summary>
        /// <remarks>
        /// The bot and Ravenfall do not share a filesystem in production, so writing the settings
        /// file only reaches a bot running on the same machine. This goes over the same HTTP
        /// transport as the other bot calls, with its retry and host failover.
        /// </remarks>
        Task SendUserSettingAsync(System.Guid userId, string key, string value);

        /// <summary>
        /// Sends the current name fields to the bot so it can move a live session to the renamed
        /// channel. Call only when a name actually changed; this is not free and the paths that
        /// would otherwise call it run on every player join.
        /// </summary>
        Task PushUserNamesAsync(System.Guid userId);
    }
}
