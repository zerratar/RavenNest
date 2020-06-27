using System.Collections.Generic;

namespace RavenNest.Health
{
    public class HealthResponse
    {
        public string Status { get; set; }
        public IDictionary<string, HealthResult> Results { get; set; }
    }
}
