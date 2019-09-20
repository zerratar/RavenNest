namespace RavenNest.BusinessLogic.Docs.Models
{
    public class DocumentApiMethodResponse
    {
        public DocumentApiMethodResponse(string contentType, string returnType, string example)
        {
            ContentType = contentType;
            ReturnType = returnType;
            Example = example;
        }

        public string ContentType { get; }
        public string ReturnType { get; }
        public string Example { get; }
    }
}