using System.Reflection;

namespace RavenNest.BusinessLogic.Docs
{
    public class DefaultGeneratorSettings : IGeneratorSettings
    {
        public string OutputFolder { get; set; }
        public Assembly Assembly { get; set; }
    }
}
