using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Docs.Models
{
    public class DocumentApiMethod
    {
        public DocumentApiMethod(
            string name,
            string method,
            string path,
            string description,
            IReadOnlyList<DocumentApiMethodParameter> parameters,
            DocumentApiMethodAuthentication authentication,
            DocumentApiMethodRequest request,
            DocumentApiMethodResponse response)
        {
            Name = name;
            Method = method;
            Path = path;
            Description = description;
            Authentication = authentication;
            RequestBody = request;
            Response = response;
            Parameters = parameters;
        }

        public string Name { get; }
        public string Method { get; }
        public string Path { get; }
        public string Description { get; }
        public IReadOnlyList<DocumentApiMethodParameter> Parameters { get; }
        public DocumentApiMethodAuthentication Authentication { get; }
        public DocumentApiMethodRequest RequestBody { get; }
        public DocumentApiMethodResponse Response { get; }

    }
}