using System.Collections.Generic;
using System.Threading.Tasks;
using RavenNest.DataModels;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IAuthManager
    {
        AuthToken Authenticate(string username, string password);
        AuthToken Get(string authToken);
        AuthToken GenerateAuthToken(User user);
        void SignUp(string userId, string userLogin, string userDisplayName, string userEmail, string password);
        bool IsAdmin(AuthToken authToken);
        string GetRandomizedBase64EncodedStateParameters(List<StateParameters> stateParameters);
        List<StateParameters> GetDecodedObjectFromState(string encodedState);
    }
}
