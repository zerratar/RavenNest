using System.Reflection;

namespace RavenNest.BusinessLogic.Docs
{
    public interface IGeneratorSettings
    {
        string OutputFolder { get; set; }
        Assembly Assembly { get; set; }
    }
}