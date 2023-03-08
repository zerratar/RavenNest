using System;

namespace RavenNest.Models
{
    public class SyntyAppearanceUpdate
    {
        public Guid PlayerId { get; set; }
        public SyntyAppearance Value { get; set; }
    }
}
