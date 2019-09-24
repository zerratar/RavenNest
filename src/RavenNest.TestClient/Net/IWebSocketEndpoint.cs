using System.Threading.Tasks;
using System.Runtime.Serialization;
using MessagePack;

namespace RavenNest.TestClient
{
    public interface IWebSocketEndpoint
    {
        Task<bool> UpdateAsync();
        Task<bool> SavePlayerAsync(IPlayerController player);
    }
}
