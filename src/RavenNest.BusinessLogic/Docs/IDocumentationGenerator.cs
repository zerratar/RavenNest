namespace RavenNest.BusinessLogic.Docs
{
    public interface IDocumentGenerator
    {
        IDocument Generate(IDocumentSettings documentSettings, IGeneratorSettings generatorSettings);
    }
}
