using System.Threading.Tasks;

namespace RavenNest.BusinessLogic
{
    public interface IRavenBotApiClient
    {
        Task SendPubSubAccessTokenAsync(string id, string login, string accessToken);
        Task SendUserRoleAsync(string userId, string userName, string v);
    }
}
