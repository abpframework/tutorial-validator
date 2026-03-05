namespace Validator.Analyst.Scraping.Models;

/// <summary>
/// Represents a single page of a tutorial.
/// </summary>
public class TutorialPage
{
    /// <summary>
    /// URL of this page.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Page title extracted from the document.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Zero-based page index in the tutorial sequence.
    /// </summary>
    public int PageIndex { get; set; }

    /// <summary>
    /// The page content converted to Markdown format.
    /// This is what the AI agent will read and interpret.
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// List of image URLs found in the page content.
    /// Can be used for vision model support.
    /// </summary>
    public List<string> ImageUrls { get; set; } = [];

    /// <summary>
    /// URL of the next page, if any.
    /// </summary>
    public string? NextPageUrl { get; set; }

    /// <summary>
    /// URL of the previous page, if any.
    /// </summary>
    public string? PreviousPageUrl { get; set; }
}
