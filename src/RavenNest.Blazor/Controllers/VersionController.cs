﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Github;
using RavenNest.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VersionController : ControllerBase
    {
        private readonly IWebHostEnvironment env;
        private readonly GameData gameData;

        public VersionController(
            IWebHostEnvironment env,
            GameData gameData)
        {
            this.env = env;
            this.gameData = gameData;
        }

        [HttpGet("check")]
        public async Task<UpdateData> GetLatestUpdateInfoAsync()
        {
            CodeOfConduct codeOfConduct = null;
            var coc = gameData.GetAllAgreements().FirstOrDefault(x => x.Type.ToLower() == "coc");
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

            var client = gameData.Client;
            var release = await Github.GetGithubReleaseAsync();
            if (release != null)
            {
                if (client.ClientVersion != release.VersionString)
                {
                    client.ClientVersion = release.VersionString;
                    client.DownloadLink = release.UpdateDownloadUrl_Win;
                }

                return new UpdateData
                {
                    Version = release.VersionString,
                    DownloadUrl = release.UpdateDownloadUrl_Win,
                    Released = DateTime.UtcNow,
                    CodeOfConduct = codeOfConduct,
                    Description = release.Description
                };
            }

            var fileName = "downloads/" + client.ClientVersion + "/update.zip";
            var path = env.WebRootPath + "\\" + fileName.Replace("/", "\\");
            var downloadLink = client.DownloadLink ?? (System.IO.File.Exists(path) ? string.Format("https://{0}/{1}", HttpContext.Request.Host, fileName) : null);

            return new UpdateData
            {
                Version = client.ClientVersion,
                DownloadUrl = downloadLink,
                Released = DateTime.UtcNow,
                CodeOfConduct = codeOfConduct,
                Description = string.Empty
            };
        }
    }
}

