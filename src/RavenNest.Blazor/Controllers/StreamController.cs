using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Twitch.Extension;

namespace RavenNest.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("api/[controller]")]
    [ApiController]
    public class StreamController : ControllerBase
    {
        private readonly ITwitchExtensionConnectionProvider extensionWsConnectionProvider;

        public StreamController(ITwitchExtensionConnectionProvider ewsConnectionProvider)
        {
            this.extensionWsConnectionProvider = ewsConnectionProvider;
        }

        [HttpGet("extension/{broadcasterId}/{sessionId}")]
        public async Task GetExtensionWebsocketConnection(string broadcasterId, string sessionId)
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                var headers = new Dictionary<string, string>
                {
                    { "broadcasterId", broadcasterId },
                    { SessionCookie.SessionCookieName, sessionId }
                };

                var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                var socketSession = extensionWsConnectionProvider.Get(webSocket, headers);
                if (socketSession == null)
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.InternalServerError,
                        "No active session",
                        CancellationToken.None);
                    return;
                }

                await HttpContext.Session.CommitAsync();
                await socketSession.KeepAlive();
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
            }
        }
    }
}
