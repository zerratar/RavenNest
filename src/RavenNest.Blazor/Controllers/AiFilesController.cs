using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;

namespace RavenNest.Controllers
{
    // NOTE: The actual /ai/download endpoint is handled as raw middleware in
    // Startup.cs (before response compression) for maximum throughput.
    // This controller only exists as a fallback and should not normally be hit
    // for the download route.
    [Route("ai")]
    public class AiFilesController : Controller
    {
        private static readonly string BasePath = @"C:\ai";
        private static readonly DateTime ExpirationDate = new DateTime(2026, 3, 30, 23, 59, 59, DateTimeKind.Utc);

        [HttpGet("download")]
        [ResponseCache(NoStore = true)]
        public IActionResult Download([FromQuery] string folder, [FromQuery] string file)
        {
            if (DateTime.UtcNow > ExpirationDate)
                return NotFound("These files are no longer available.");

            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(file))
                return BadRequest("Missing folder or file parameter.");

            var safeFolder = Path.GetFileName(folder);
            var safeFile = Path.GetFileName(file);
            var filePath = Path.Combine(BasePath, safeFolder, safeFile);

            if (!System.IO.File.Exists(filePath))
                return NotFound("File not found.");

            if (!filePath.EndsWith(".safetensors", StringComparison.OrdinalIgnoreCase))
                return NotFound("File not found.");

            return PhysicalFile(filePath, "application/octet-stream", safeFile, enableRangeProcessing: true);
        }
    }
}
