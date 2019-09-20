using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Docs
{
    public interface IDocumentProcessor
    {
        Task ProcessAsync(IGeneratorSettings settings, IDocument document);
    }
}