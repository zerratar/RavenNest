using System;

namespace RavenNest.BusinessLogic.Docs.Attributes
{
    public class ApiDescriptorAttribute : Attribute
    {
        public ApiDescriptorAttribute()
        {
        }

        public string Name { get; set; }
        public string Description { get; set; }
    }
}
