using RavenNest.Models;
using RavenNest.Sessions;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Twitch.Extension
{
    public interface IExtensionConnection
    {
        void EnqueueSend<T>(T data);
        Task SendAsync<T>(T data);
        Task<T> ReceiveAsync<T>();
        Task SendAsync(Packet packet);
        void Close();
        bool Closed { get; }
        Task KeepAlive();
        SessionInfo Session { get; }
        string BroadcasterTwitchUserId { get; }
    }
}
