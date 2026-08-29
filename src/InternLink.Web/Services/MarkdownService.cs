using Markdig;

namespace InternLink.Web.Services;

public interface IMarkdownService
{
    string RenderToHtml(string? markdown);
}

public class MarkdownService : IMarkdownService
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownService()
    {
        // DisableHtml() ensures raw HTML in the markdown renders as escaped inert text,
        // neutralizing XSS and script injection attacks.
        _pipeline = new MarkdownPipelineBuilder()
            .DisableHtml()
            .UseAutoLinks()
            .UseEmphasisExtras()
            .Build();
    }

    public string RenderToHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        return Markdown.ToHtml(markdown, _pipeline);
    }
}
