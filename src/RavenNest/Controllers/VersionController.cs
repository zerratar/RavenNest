using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RavenNest.BusinessLogic.Data;
using RavenNest.Models;
using System;
using System.Threading.Tasks;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VersionController : ControllerBase
    {
        private readonly IHostingEnvironment env;
        private readonly IRavenfallDbContextProvider dbProvider;

        public VersionController(
            IHostingEnvironment env,
            IRavenfallDbContextProvider dbProvider)
        {
            this.env = env;
            this.dbProvider = dbProvider;
        }

        [HttpGet("check")]
        public async Task<UpdateData> GetLatestUpdateInfoAsync()
        {
            using (var db = dbProvider.Get())
            {
                var clientInfo = await db.GameClient.FirstOrDefaultAsync();
                var fileName = "downloads/" + clientInfo.ClientVersion + "/update.zip";
                if (clientInfo == null) return null;
                var path = env.WebRootPath + "\\" + fileName.Replace("/", "\\");
                if (!System.IO.File.Exists(path)) return null;
                return new UpdateData
                {
                    Version = clientInfo.ClientVersion,
                    DownloadUrl = string.Format("https://{0}/{1}", HttpContext.Request.Host, fileName),
                    Released = DateTime.UtcNow
                };
            }
        }
    }
}

