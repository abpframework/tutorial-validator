namespace Validator.Analyst.Scraping.Models;

/// <summary>
/// Root container for scraped tutorial content.
/// Contains all pages and metadata from a multi-page tutorial.
/// </summary>
public class ScrapedTutorial
{
    /// <summary>
    /// Title of the tutorial (from the first page).
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Original URL that was used to start scraping.
    /// </summary>
    public required string SourceUrl { get; set; }

    /// <summary>
    /// Timestamp when the scraping was performed.
    /// </summary>
    public DateTime ScrapedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// All pages in the tutorial, in order.
    /// Each page contains markdown content ready for AI interpretation.
    /// </summary>
    public required List<TutorialPage> Pages { get; set; }

    /// <summary>
    /// Total number of pages in the tutorial.
    /// </summary>
    public int TotalPages => Pages.Count;
}
