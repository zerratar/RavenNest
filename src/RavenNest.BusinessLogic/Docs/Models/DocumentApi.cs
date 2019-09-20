using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Docs.Models
{
    public class DocumentApi
    {
        public DocumentApi(string name, string description, string path, IReadOnlyList<DocumentApiMethod> methods)
        {
            Name = name;
            Methods = methods;
            Description = description;
            Path = path;
        }

        public string Name { get; }
        public string Description { get; }
        public string Path { get; }
        public IReadOnlyList<DocumentApiMethod> Methods { get; }
    }
}