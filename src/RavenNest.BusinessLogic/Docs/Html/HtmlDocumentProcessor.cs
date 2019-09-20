using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RavenNest.BusinessLogic.Docs.Html
{
    public class HtmlDocumentProcessor : IDocumentProcessor
    {
        public async Task ProcessAsync(IGeneratorSettings settings, IDocument document)
        {
            var data = JSON.Stringify(document);
            var folder = System.IO.Path.Combine(settings.OutputFolder, "assets");
            var filePath = System.IO.Path.Combine(folder, "documentation.data.js");
            if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);
            await System.IO.File.WriteAllTextAsync(filePath, "var data = " + data + ";");
        }
    }
}
