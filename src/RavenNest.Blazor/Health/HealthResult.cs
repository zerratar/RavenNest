using System.Collections.Generic;

namespace RavenNest.Health
{
    public class HealthResult
    {
        public string Status { get; set; }
        public string Description { get; set; }
        public IReadOnlyDictionary<string, object> Data { get; set; }
    }
}
