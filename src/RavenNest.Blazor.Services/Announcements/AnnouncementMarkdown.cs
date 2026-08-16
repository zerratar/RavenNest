using Markdig;

namespace RavenNest.Blazor.Services.Announcements
{
    /// <summary>
    ///     Turns a post's markdown into HTML.
    /// </summary>
    /// <remarks>
    ///     One pipeline, shared by the page that renders a post and the preview in the admin panel.
    ///     A preview that renders through a different pipeline than the real page is worse than no
    ///     preview, because it is confidently wrong.
    ///
    ///     <para>
    ///     Raw HTML is dropped rather than passed through. Only administrators can write these, but
    ///     "only an administrator can inject script into every visitor's page" is not a security
    ///     model, and nothing a post needs to say requires raw HTML.
    ///     </para>
    /// </remarks>
    public static class AnnouncementMarkdown
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .DisableHtml()
            .UseAutoLinks()
            .UsePipeTables()
            .Build();

        public static string ToHtml(string markdown)
        {
            return string.IsNullOrWhiteSpace(markdown) ? "" : Markdown.ToHtml(markdown, Pipeline);
        }
    }
}
