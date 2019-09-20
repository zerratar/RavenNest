using System.Collections.Generic;
using RavenNest.BusinessLogic.Docs.Models;

namespace RavenNest.BusinessLogic.Docs
{
    internal class Document : IDocument
    {
        public IDocumentSettings Settings { get; internal set; }

        public IReadOnlyList<DocumentPage> Pages { get; internal set; }

        public IReadOnlyList<DocumentApi> Apis { get; internal set; }
    }
}