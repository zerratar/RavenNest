using System.Threading.Tasks;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IAuthManager
    {
        Task<AuthToken> AuthenticateAsync(string username, string password);
        AuthToken Get(string authToken);
        Task SignUpAsync(string userId, string userLogin, string userDisplayName, string userEmail, string password);
        Task<bool> CheckIfIsAdminAsync(AuthToken authToken);
    }
}