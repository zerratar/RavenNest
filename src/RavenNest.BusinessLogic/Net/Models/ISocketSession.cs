using RavenNest.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Net
{
    public interface IGameWebSocketConnection : IDisposable
    {
        SessionToken SessionToken { get; }

        Task<TResult> SendAsync<TResult>(
            string id,
            object request,
            CancellationToken cancellationToken);

        Task ReplyAsync(
            Guid correlationId,
            string id,
            object request,
            CancellationToken cancellationToken);

        Task ReplyAsync(GamePacket packet, object request);
        Task ReplyAsync(GamePacket packet, object request, CancellationToken cancellationToken);

        Task<bool> PushAsync(string id, object request, CancellationToken cancellationToken);

        Task KeepAlive();
    }
}
