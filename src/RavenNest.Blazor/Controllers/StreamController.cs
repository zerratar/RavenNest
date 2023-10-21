using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Twitch.Extension;

namespace RavenNest.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("api/[controller]")]
    [ApiController]
    public class StreamController : ControllerBase
    {
        private readonly ILogger<StreamController> logger;
        private readonly ITwitchExtensionConnectionProvider extensionWsConnectionProvider;
        private readonly WebSocketAcceptContext acceptContext;

        public StreamController(
            ILogger<StreamController> logger,
            ITwitchExtensionConnectionProvider ewsConnectionProvider)
        {
            this.logger = logger;
            this.extensionWsConnectionProvider = ewsConnectionProvider;
            this.acceptContext = new WebSocketAcceptContext();

            acceptContext.KeepAliveInterval = TimeSpan.FromSeconds(30);
        }

        [HttpGet("extension/{broadcasterId}/{sessionId}")]
        public async Task GetExtensionWebsocketConnection(string broadcasterId, string sessionId)
        {
            try
            {
                var wsReq = HttpContext.WebSockets.IsWebSocketRequest;
                logger.LogError($"Getting Websocket Request for {broadcasterId}/{sessionId}, WS: " + wsReq);

                if (wsReq)
                {

                    var headers = new Dictionary<string, string>
                    {
                        { "broadcasterId", broadcasterId },
                        { SessionCookie.SessionCookieName, sessionId }
                    };

                    var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync(acceptContext);
                    var socketSession = extensionWsConnectionProvider.Get(webSocket, headers);
                    if (socketSession == null)
                    {
                        logger.LogError($"Unable to start extension websocket using {broadcasterId}/{sessionId}: Socket Session is null!");

                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.InternalServerError,
                            "No active session",
                            CancellationToken.None);

                        return;
                    }

                    await HttpContext.Session.CommitAsync();
                    await socketSession.KeepAlive();

                    logger.LogError($"Twitch Extension WS Closing for {broadcasterId}/{sessionId}");
                }
                else
                {
                    HttpContext.Response.StatusCode = 400;
                }
            }
            catch (Exception exc)
            {
                logger.LogError($"Unable to start extension websocket using {broadcasterId}/{sessionId}: " + exc);
            }
        }
    }
}
