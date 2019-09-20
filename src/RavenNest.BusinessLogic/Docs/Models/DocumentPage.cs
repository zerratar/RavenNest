namespace RavenNest.BusinessLogic.Docs.Models
{
    public class DocumentPage
    {
        public DocumentPage(string name, string content)
        {
            Name = name;
            Content = content;
        }

        public string Name { get; }
        public string Content { get; }
    }
}