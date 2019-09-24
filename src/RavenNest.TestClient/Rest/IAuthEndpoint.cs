using RavenNest.Models;
using System.Threading.Tasks;

namespace RavenNest.TestClient.Rest
{
    public interface IAuthEndpoint
    {
        Task<AuthToken> AuthenticateAsync(string username, string password);
    }
}
