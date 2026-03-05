using AngleSharp;
using ReverseMarkdown;
using Validator.Analyst.Scraping.Models;

namespace Validator.Analyst.Scraping;

/// <summary>
/// HTML parser facade using AngleSharp and ReverseMarkdown.
/// Delegates to ContentExtractor, NavigationExtractor, and MarkdownCleaner.
/// </summary>
public class HtmlParser : IHtmlParser
{
    private readonly IBrowsingContext _context;
    private readonly Converter _markdownConverter;

    /// <summary>
    /// Creates a new HtmlParser with default configuration.
    /// </summary>
    public HtmlParser()
    {
        var config = Configuration.Default;
        _context = BrowsingContext.New(config);

        _markdownConverter = new Converter(new Config
        {
            GithubFlavored = true,
            RemoveComments = true,
            SmartHrefHandling = true,
            UnknownTags = Config.UnknownTagsOption.Bypass
        });
    }

    /// <inheritdoc/>
    public ParsedPageResult Parse(string html, string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new ScrapingException("HTML content is empty", sourceUrl);
        }

        var document = _context.OpenAsync(req => req.Content(html).Address(sourceUrl))
            .GetAwaiter().GetResult();

        var title = ContentExtractor.ExtractTitle(document);
        
        // Extract navigation BEFORE cleaning (navigation links might be in areas we clean)
        var (nextUrl, prevUrl) = NavigationExtractor.ExtractNavigationLinks(document, sourceUrl);
        
        var contentArea = ContentExtractor.FindContentArea(document);
        var imageUrls = ContentExtractor.ExtractImageUrls(contentArea, sourceUrl);

        // Clean the content area before conversion
        ContentExtractor.CleanContentArea(contentArea);

        // Convert to markdown
        var contentHtml = contentArea?.InnerHtml ?? "";
        var markdown = _markdownConverter.Convert(contentHtml);

        // Clean up the markdown (remove noise, variant links, excessive whitespace)
        markdown = MarkdownCleaner.CleanMarkdown(markdown);
        markdown = MarkdownCleaner.RemoveVariantLinks(markdown);

        return new ParsedPageResult
        {
            Title = title,
            Content = markdown,
            ImageUrls = imageUrls,
            NextPageUrl = nextUrl,
            PreviousPageUrl = prevUrl
        };
    }
}
