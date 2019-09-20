using System;

namespace RavenNest.BusinessLogic.Docs.Attributes
{
    public class MethodDescriptorAttribute : Attribute
    {
        public MethodDescriptorAttribute()
        {
        }

        public string Name { get; set; }
        public string Description { get; set; }
        public string ResponseExample { get; set; }
        public string RequestExample { get; set; }
        public bool RequiresAuth { get; set; }
        public bool RequiresAdmin { get; set; }
        public bool RequiresTwitchAuth { get; set; }
        public bool RequiresSession { get; set; }
    }
}
