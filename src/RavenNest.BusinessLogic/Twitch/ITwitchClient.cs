using System.Collections.Generic;
using System.Threading.Tasks;
using TwitchLib.Api.Helix.Models.Subscriptions;

namespace RavenNest.BusinessLogic.Game
{
    public interface ITwitchClient
    {
        Task<Subscription> GetSubscriberAsync(string userId);

        /// <summary>
        /// Resolves the current login and display name for a Twitch account id. Returns null when
        /// Twitch cannot be reached or the account no longer exists, so callers can carry on with
        /// whatever name they already had.
        /// </summary>
        Task<RavenNest.Twitch.TwitchRequests.TwitchUser> GetUserByIdAsync(string twitchUserId);

        /// <summary>
        /// Batched form of <see cref="GetUserByIdAsync"/>. Accounts Twitch does not return, for
        /// example ones that were deleted, are simply absent from the result. Returns an empty list
        /// rather than null when the lookup fails.
        /// </summary>
        Task<IReadOnlyList<RavenNest.Twitch.TwitchRequests.TwitchUser>> GetUsersByIdAsync(
            IReadOnlyList<string> twitchUserIds);
    }
}