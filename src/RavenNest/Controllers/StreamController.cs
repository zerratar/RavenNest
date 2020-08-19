using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RavenNest.BusinessLogic.Net;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StreamController : ControllerBase
    {
        private readonly IWebSocketConnectionProvider wsConnectionProvider;

        public StreamController(IWebSocketConnectionProvider wsConnectionProvider)
        {
            this.wsConnectionProvider = wsConnectionProvider;
        }

        [HttpGet]
        public async Task Get()
        {
            var socketSessionProvider = wsConnectionProvider;
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                var socketSession = socketSessionProvider.Get(
                    webSocket, HttpContext.Request.Headers.ToDictionary(x => x.Key, y => string.Join(",", y.Value)));

                if (socketSession == null)
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.InternalServerError,
                        "No active session",
                        CancellationToken.None);
                    return;
                }

                await socketSession.KeepAlive();
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
            }
        }
    }
}