using System.Collections.Generic;

namespace RavenNest.Health
{
    public class HealthResponse
    {
        public string Status { get; set; }

        public IList<HealthResult> Results { get; set; }
    }
}
