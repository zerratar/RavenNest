namespace RavenNest.BusinessLogic.Docs.Models
{
    public class DocumentApiMethodParameter
    {
        public DocumentApiMethodParameter(string type, string name, string description, string @default, bool optional)
        {
            Type = type;
            Name = name;
            Description = description;
            Default = @default;
            Optional = optional;
        }
        public string Type { get; }
        public string Name { get; }
        public string Description { get; }
        public string Default { get; }
        public bool Optional { get; }
    }
}