using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface ISecureHasher
    {
        string Get(AuthToken token);
        string Get(SessionToken token);
        string Get(string text);
    }
}