using System.Collections.Generic;
using System.Threading.Tasks;
using TwitchLib.Api.Helix.Models.Subscriptions;

namespace RavenNest.BusinessLogic.Game
{
    public interface ITwitchClient
    {
        Task<Subscription> GetSubscriberAsync(string userId);
    }
}