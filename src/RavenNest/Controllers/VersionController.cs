using Microsoft.AspNetCore.Mvc;
using RavenNest.Models;
using System.Threading.Tasks;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VersionController : ControllerBase
    {
        [HttpGet("check")]
        public async Task<UpdateData> GetLatestUpdateInfoAsync()
        {



            return null;
        }
    }
}

