namespace Validator.Analyst.Scraping.Models;

/// <summary>
/// Result of parsing an HTML page.
/// </summary>
public class ParsedPageResult
{
    /// <summary>
    /// Page title extracted from the document.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// The page content converted to Markdown format.
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// List of image URLs found in the content.
    /// </summary>
    public List<string> ImageUrls { get; set; } = [];

    /// <summary>
    /// URL of the next page in the tutorial, if detected.
    /// </summary>
    public string? NextPageUrl { get; set; }

    /// <summary>
    /// URL of the previous page in the tutorial, if detected.
    /// </summary>
    public string? PreviousPageUrl { get; set; }
}
