using System.Threading.Tasks;

namespace RavenNest.BusinessLogic
{
    public interface IRavenBotApiClient
    {
        Task UpdateUserSettingsAsync(System.Guid userId);
    }
}
