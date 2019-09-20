using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Docs.Models
{
    public class DocumentApiMethodRequest
    {
        public DocumentApiMethodRequest(string contentType, string example)
        {
            this.ContentType = contentType;
            Example = example;
        }

        public string ContentType { get; }
        public string Example { get; }
    }
}