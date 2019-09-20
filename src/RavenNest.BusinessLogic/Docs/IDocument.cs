using System.Collections.Generic;
using RavenNest.BusinessLogic.Docs.Models;

namespace RavenNest.BusinessLogic.Docs
{
    public interface IDocument
    {
        IDocumentSettings Settings { get; }
        IReadOnlyList<DocumentPage> Pages { get; }
        IReadOnlyList<DocumentApi> Apis { get; }
    }
}