using System.Threading.Tasks;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IAuthManager
    {
        AuthToken Authenticate(string username, string password);
        AuthToken Get(string authToken);
        void SignUp(string userId, string userLogin, string userDisplayName, string userEmail, string password);
        bool IsAdmin(AuthToken authToken);
    }
}