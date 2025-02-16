using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace ManhwaZoneWeb.Services
{
    public interface IIPBlockingService
    {
        void Block(IPAddress iPAddress);
        bool IsBlocked(IPAddress iPAddress);
    }
    public class IPBlockingService : IIPBlockingService
    {
        private readonly ILogger<IPBlockingService> logger;
        private HashSet<string> blacklisted = new HashSet<string>();

        public IPBlockingService(ILogger<IPBlockingService> logger)
        {
            if (File.Exists("blacklist.txt"))
            {
                foreach (var ip in File.ReadAllLines("blacklist.txt"))
                {
                    blacklisted.Add(ip);
                }
            }

            this.logger = logger;
        }

        public void Block(IPAddress ip)
        {
            if (blacklisted.Add(ip.ToString()))
            {
                logger.LogWarning("IP: " + ip.ToString() + " has been blocked.");
                SaveBlacklist();
            }
        }

        public bool IsBlocked(IPAddress ip)
        {
            return blacklisted.Contains(ip.ToString());
        }

        private void SaveBlacklist()
        {
            File.WriteAllLines("blacklist.txt", blacklisted.ToArray());
        }
    }
    public class IPBlockMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IIPBlockingService _blockingService;
        public IPBlockMiddleware(RequestDelegate next, IIPBlockingService blockingService)
        {
            _next = next;
            _blockingService = blockingService;
        }

        public async Task Invoke(HttpContext context)
        {
            var remoteIp = context.Connection.RemoteIpAddress;
            var isBlocked = _blockingService.IsBlocked(remoteIp!);

            if (!isBlocked)
            {
                // check if we are requesting suspicious endpoints. block ip.
                if (context.Request.Path.ToUriComponent().Contains("wlwmanifest.xml", StringComparison.OrdinalIgnoreCase))
                {
                    isBlocked = true;
                    _blockingService.Block(remoteIp!);
                }
            }

            if (isBlocked)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            await _next.Invoke(context);
        }
    }
}
