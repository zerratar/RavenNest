using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic
{
    public interface IRavenBotApiClient
    {
        //Task SendTwitchPubSubAccessTokenAsync(string id, string login, string accessToken);
        //Task SendUserRoleAsync(string userId, string platform, string userName, string v);
        void UpdateUserSettings(System.Guid userId);
    }
}
