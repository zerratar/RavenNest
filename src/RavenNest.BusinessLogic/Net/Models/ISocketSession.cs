using RavenNest.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Net
{
    public interface IWebSocketConnection
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

        Task<bool> PushAsync(string id, object request, CancellationToken cancellationToken);

        Task KeepAlive();
    }
}