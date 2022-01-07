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
        private readonly IWebHostEnvironment env;
        private readonly IRavenfallDbContextProvider dbProvider;

        public VersionController(
            IWebHostEnvironment env,
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
                var clientInfo = await db.GameClient.FirstAsync();
                var fileName = "downloads/" + clientInfo.ClientVersion + "/update.zip";
                var path = env.WebRootPath + "\\" + fileName.Replace("/", "\\");
                var downloadLink = clientInfo.DownloadLink ?? (System.IO.File.Exists(path) ? string.Format("https://{0}/{1}", HttpContext.Request.Host, fileName) : null);

                CodeOfConduct codeOfConduct = null;

                var coc = await db.Agreements.FirstOrDefaultAsync(x => x.Type.ToLower() == "coc");
                if (coc != null)
                {
                    codeOfConduct = new CodeOfConduct
                    {
                        LastModified = coc.LastModified.GetValueOrDefault(),
                        Message = coc.Message,
                        Revision = coc.Revision,
                        Title = coc.Title,
                        VisibleInClient = coc.VisibleInClient
                    };
                }

                return new UpdateData
                {
                    Version = clientInfo.ClientVersion,
                    DownloadUrl = downloadLink,
                    Released = DateTime.UtcNow,
                    CodeOfConduct = codeOfConduct
                };
            }
        }
    }
}

